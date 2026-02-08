using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimpleGsxIntegrator
{
    public sealed class Pmdg737Controller
    {
        private readonly SimConnect _sim;
        private readonly SimVarMonitor? _simVarMonitor;

        private const uint OPEN_PARAM = 1;
        private const uint CLOSE_PARAM = 2;

        public bool IsConnected { get; private set; }

        private enum DATA_ID : uint
        {
            DATA = 0x4E473331,
            CONTROL = 0x4E473333
        }

        private enum DEF_ID : uint
        {
            DATA = 0x4E473332,
            CONTROL = 0x4E473334
        }

        private enum REQ_ID : uint
        {
            DATA = 1,
            CONTROL = 2
        }

        private const uint BASE = 0x00011000;

        private const uint EVT_DOOR_FWD_L = BASE + 14005;
        private const uint EVT_DOOR_FWD_R = BASE + 14006;
        private const uint EVT_DOOR_AFT_L = BASE + 14007;
        private const uint EVT_DOOR_AFT_R = BASE + 14008;
        private const uint EVT_DOOR_CARGO_FWD = BASE + 14013;
        private const uint EVT_DOOR_CARGO_AFT = BASE + 14014;
        private const uint EVT_OH_ELEC_GRD_PWR_SWITCH = BASE + 17;
        private enum PMDG_EVENTS : uint { OH_ELEC_GRD_PWR_SWITCH = BASE + 17 }
        private enum EVENT_GROUP : uint { GROUP0 = 0 }

        public Pmdg737Controller(SimConnect sim, SimVarMonitor? simVarMonitor = null)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            _simVarMonitor = simVarMonitor;
        }

        public void Connect()
        {
            try
            {
                Logger.Debug("PMDG: Initializing SDK");

                _sim.MapClientDataNameToID("PMDG_NG3_Control", DATA_ID.CONTROL);

                uint sizeControl = (uint)Marshal.SizeOf<PMDG_NG3_Control>();
                _sim.AddToClientDataDefinition(DEF_ID.CONTROL, 0, sizeControl, 0, 0);
                _sim.RegisterDataDefineStruct<PMDG_NG3_Control>(DEF_ID.CONTROL);

                _sim.RequestClientData(DATA_ID.CONTROL, REQ_ID.CONTROL, DEF_ID.CONTROL,
                    SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

                IsConnected = true;
                Logger.Debug("PMDG SDK connected!");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Logger.Error($"PMDG SDK init failed: {ex}");
            }
        }

        public Task CloseAllDoors()
        {
            return Task.Run(async () =>
            {
                Logger.Info("Closing all Doors");
                await Close(EVT_DOOR_FWD_L);
                await Close(EVT_DOOR_FWD_R);
                await Close(EVT_DOOR_AFT_L);
                await Close(EVT_DOOR_AFT_R);
                await Close(EVT_DOOR_CARGO_FWD);
                await Close(EVT_DOOR_CARGO_AFT);
            });
        }

        public Task OpenDoorsForBoarding()
        {
            return Task.Run(async () =>
            {
                try
                {
                    uint[] doors = new[] { EVT_DOOR_FWD_L, EVT_DOOR_AFT_L, EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT };
                    Logger.Info("Opening Boarding Doors");
                    foreach (var door in doors)
                    {
                        if (!IsDoorOpen(door))
                        {
                            await Open(door);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"OpenDoorsForBoarding failed: {ex}");
                }
            });
        }

        public Task OpenDoorsForCatering()
        {
            return Task.Run(async () =>
            {
                try
                {
                    Logger.Info("Opening Catering Doors");
                    await Open(EVT_DOOR_FWD_R);
                    await Open(EVT_DOOR_AFT_R);
                }
                catch (Exception ex)
                {
                    Logger.Error($"OpenDoorsForCatering failed: {ex}");
                }
            });
        }

        public Task CloseOpenDoors()
        {
            return Task.Run(async () =>
            {
                try
                {
                    Logger.Info("Closing Open Doors");
                    if (IsDoorOpen(EVT_DOOR_FWD_L))
                        await Close(EVT_DOOR_FWD_L);

                    if (IsDoorOpen(EVT_DOOR_FWD_R))
                        await Close(EVT_DOOR_FWD_R);

                    if (IsDoorOpen(EVT_DOOR_AFT_L))
                        await Close(EVT_DOOR_AFT_L);

                    if (IsDoorOpen(EVT_DOOR_AFT_R))
                        await Close(EVT_DOOR_AFT_R);

                    if (IsDoorOpen(EVT_DOOR_CARGO_FWD))
                        await Close(EVT_DOOR_CARGO_FWD);

                    if (IsDoorOpen(EVT_DOOR_CARGO_AFT))
                        await Close(EVT_DOOR_CARGO_AFT);
                }
                catch (Exception ex)
                {
                    Logger.Error($"CloseOpenDoors failed: {ex}");
                }
            });
        }

        public Task DisconnectGpu()
        {
            return Task.Run(async () =>
            {
                if (!IsConnected)
                {
                    Logger.Debug("Can't disconnect GPU: PMDG SDK not connected");
                    return;
                }

                try
                {
                    Logger.Info("Disconnecting GPU");

                    await SendCommand(EVT_OH_ELEC_GRD_PWR_SWITCH, 0);
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Logger.Error($"DisconnectGpu failed: {ex}");
                }
            });
        }

        private async Task<bool> Close(uint door)
        {
            if (!IsConnected)
            {
                Logger.Error("PMDG not connected");
                return false;
            }

            if (!IsDoorOpen(door))
            {
                Logger.Debug($"PMDG: door '{GetDoorName(door)}' already closed");
                return true;
            }

            return await SendCommand(door, CLOSE_PARAM);
        }

        private async Task<bool> Open(uint door)
        {
            if (!IsConnected)
            {
                Logger.Error("PMDG not connected");
                return false;
            }

            if (IsDoorOpen(door))
            {
                Logger.Debug($"PMDG: door '{GetDoorName(door)}' already open");
                return true;
            }

            return await SendCommand(door, OPEN_PARAM);
        }

        private bool IsDoorOpen(uint door)
        {
            if (_simVarMonitor == null) return false;

            double val = double.NaN;
            switch (door)
            {
                case EVT_DOOR_FWD_L:
                    val = _simVarMonitor.FwdLeftCabinDoor;
                    break;
                case EVT_DOOR_AFT_L:
                    val = _simVarMonitor.AftLeftCabinDoor;
                    break;
                case EVT_DOOR_CARGO_FWD:
                    val = _simVarMonitor.FwdLwrCargoDoor;
                    break;
                case EVT_DOOR_CARGO_AFT:
                    val = _simVarMonitor.AftLwrCargoDoor;
                    break;
                case EVT_DOOR_FWD_R:
                    val = _simVarMonitor.FwdRightCabinDoor;
                    break;
                case EVT_DOOR_AFT_R:
                    val = _simVarMonitor.AftRightCabinDoor;
                    break;
                default:
                    return false;
            }

            if (double.IsNaN(val)) return false;

            if (val >= 0.50) return true;
            else return false;
        }

        private async Task<bool> SendCommand(uint evt, uint param)
        {
            var cmd = new PMDG_NG3_Control { Event = evt, Parameter = param };
            try
            {
                _sim.SetClientData(DATA_ID.CONTROL, DEF_ID.CONTROL, SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT, 0, cmd);
                Logger.Debug($"PMDG send evt={evt} ({GetDoorName(evt)})");
            }
            catch (Exception ex)
            {
                Logger.Error($"PMDG send failed: {ex}");
                return false;
            }

            return true;
        }

        private string GetDoorName(uint evt)
        {
            return evt switch
            {
                EVT_DOOR_FWD_L => "Forward Left Cabin",
                EVT_DOOR_FWD_R => "Forward Right Cabin",
                EVT_DOOR_AFT_L => "Aft Left Cabin",
                EVT_DOOR_AFT_R => "Aft Right Cabin",
                EVT_DOOR_CARGO_FWD => "Forward Cargo",
                EVT_DOOR_CARGO_AFT => "Aft Cargo",
                _ => $"evt_{evt}"
            };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PMDG_NG3_Control
    {
        public uint Event;
        public uint Parameter;
    }
}
