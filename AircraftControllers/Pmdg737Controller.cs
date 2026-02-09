using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimpleGsxIntegrator
{
    public sealed class Pmdg737Controller
    {
        private readonly SimConnect _simConnect;
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
        private const uint EVT_DOOR_CARGO_MAIN = BASE + 14015;
        private const uint EVT_DOOR_EQUIPMENT_HATCH = BASE + 14016;
        private const uint EVT_OH_ELEC_GRD_PWR_SWITCH = BASE + 17;

        private const uint EVT_CDU_R_L1 = BASE + 606;
        private const uint EVT_CDU_R_L2 = BASE + 607;
        private const uint EVT_CDU_R_L3 = BASE + 608;
        private const uint EVT_CDU_R_L4 = BASE + 609;
        private const uint EVT_CDU_R_L5 = BASE + 610;
        private const uint EVT_CDU_R_L6 = BASE + 611;
        private const uint EVT_CDU_R_R1 = BASE + 612;
        private const uint EVT_CDU_R_R2 = BASE + 613;
        private const uint EVT_CDU_R_R3 = BASE + 614;
        private const uint EVT_CDU_R_R4 = BASE + 615;
        private const uint EVT_CDU_R_R5 = BASE + 616;
        private const uint EVT_CDU_R_R6 = BASE + 617;
        private const uint EVT_CDU_R_MENU = BASE + 623;
        private enum PMDG_EVENTS : uint { OH_ELEC_GRD_PWR_SWITCH = BASE + 17 }
        private enum EVENT_GROUP : uint { GROUP0 = 0 }

        public Pmdg737Controller(SimConnect sim, SimVarMonitor? simVarMonitor = null)
        {
            _simConnect = sim ?? throw new ArgumentNullException(nameof(sim));
            _simVarMonitor = simVarMonitor;
            GroundPowerLightConnected = double.NaN;
        }

        public double GroundPowerLightConnected { get; private set; }
        public double FwdLeftCabinDoor { get; private set; }
        public double FwdLeftCabinDoorFlag { get; private set; }
        public double AftLeftCabinDoor { get; private set; }
        public double AftLeftCabinDoorFlag { get; private set; }
        public double FwdRightCabinDoor { get; private set; }
        public double FwdRightCabinDoorFlag { get; private set; }
        public double AftRightCabinDoor { get; private set; }
        public double AftRightCabinDoorFlag { get; private set; }
        public double FwdLwrCargoDoor { get; private set; }
        public double AftLwrCargoDoor { get; private set; }
        public double MainCargoDoor { get; private set; }
        public double EquipmentHatchDoor { get; private set; }
        public double Chocks { get; private set; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct PmdgVarsStruct
        {
            public double FwdLeftCabinDoor;
            public double FwdLeftCabinDoorFlag;
            public double AftLeftCabinDoor;
            public double AftLeftCabinDoorFlag;
            public double FwdRightCabinDoor;
            public double FwdRightCabinDoorFlag;
            public double AftRightCabinDoor;
            public double AftRightCabinDoorFlag;
            public double FwdLwrCargoDoor;
            public double AftLwrCargoDoor;
            public double MainCargoDoor;
            public double EEDoor;
            public double GroundPowerLightConnected;
            public double Chocks;
        }

        public void Connect()
        {
            try
            {
                Logger.Debug("PMDG: Initializing SDK");

                _simConnect.MapClientDataNameToID("PMDG_NG3_Control", DATA_ID.CONTROL);

                uint sizeControl = (uint)Marshal.SizeOf<PMDG_NG3_Control>();
                _simConnect.AddToClientDataDefinition(DEF_ID.CONTROL, 0, sizeControl, 0, 0);
                _simConnect.RegisterDataDefineStruct<PMDG_NG3_Control>(DEF_ID.CONTROL);

                _simConnect.RequestClientData(DATA_ID.CONTROL, REQ_ID.CONTROL, DEF_ID.CONTROL,
                    SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

                try
                {
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:FwdLeftCabinDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:FwdLeftCabinDoorFlag", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:AftLeftCabinDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:AftLeftCabinDoorFlag", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:FwdRightCabinDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:FwdRightCabinDoorFlag", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:AftRightCabinDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:AftRightCabinDoorFlag", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:FwdLwrCargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:AftLwrCargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:MainCargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:EEDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:7X7X_Ground_Power_Light_Connected", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar,
                        "L:NGXWheelChocks", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                    _simConnect.RegisterDataDefineStruct<PmdgVarsStruct>(DEFINITIONS.PmdgVar);

                    _simConnect.RequestDataOnSimObject(DATA_REQUESTS.PmdgVar, DEFINITIONS.PmdgVar,
                        SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND,
                        SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"PMDG var registration failed: {ex.Message}");
                }

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
                await Close(EVT_DOOR_CARGO_MAIN);
                await Close(EVT_DOOR_EQUIPMENT_HATCH);
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
                    var doors = new[] { EVT_DOOR_FWD_L, EVT_DOOR_FWD_R, EVT_DOOR_AFT_L, EVT_DOOR_AFT_R, EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT, EVT_DOOR_CARGO_MAIN, EVT_DOOR_EQUIPMENT_HATCH };
                    bool doorsToClose = false;

                    foreach (var door in doors)
                    {
                        if (IsDoorOpen(door))
                        {
                            doorsToClose = true;
                            await Close(door);
                        }
                    }

                    if (doorsToClose)
                    {
                        Logger.Info("Closed Open Doors");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"CloseOpenDoors failed: {ex}");
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
            double val = double.NaN;

            switch (door)
            {
                case EVT_DOOR_FWD_L:
                    val = FwdLeftCabinDoor;
                    break;
                case EVT_DOOR_AFT_L:
                    val = AftLeftCabinDoor;
                    break;
                case EVT_DOOR_CARGO_FWD:
                    val = FwdLwrCargoDoor;
                    break;
                case EVT_DOOR_CARGO_AFT:
                    val = AftLwrCargoDoor;
                    break;
                case EVT_DOOR_CARGO_MAIN:
                    val = MainCargoDoor;
                    break;
                case EVT_DOOR_EQUIPMENT_HATCH:
                    val = EquipmentHatchDoor;
                    break;
                case EVT_DOOR_FWD_R:
                    val = FwdRightCabinDoor;
                    break;
                case EVT_DOOR_AFT_R:
                    val = AftRightCabinDoor;
                    break;
                default:
                    return false;
            }

            if (double.IsNaN(val)) return false;

            return val >= 0.50;
        }

        private async Task<bool> SendCommand(uint evt, uint param)
        {
            var cmd = new PMDG_NG3_Control { Event = evt, Parameter = param };
            try
            {
                _simConnect.SetClientData(DATA_ID.CONTROL, DEF_ID.CONTROL, SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT, 0, cmd);
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
                EVT_DOOR_CARGO_MAIN => "Main Cargo",
                EVT_DOOR_EQUIPMENT_HATCH => "Equipment Hatch",
                _ => $"evt_{evt}"
            };
        }

        public Task RemoveGroundEquipment()
        {
            return Task.Run(async () =>
            {
                if (!IsConnected)
                {
                    Logger.Debug("Can't trigger FMC sequence: PMDG SDK not connected");
                    return;
                }

                try
                {
                    bool chocksSet = !double.IsNaN(Chocks) && Chocks > 0.5;

                    if (!chocksSet)
                    {
                        Logger.Debug("Chocks not set, skipping FMC sequence");
                        return;
                    }

                    Logger.Info("Removing Chocks");

                    await SendCommand(EVT_CDU_R_MENU, 1);
                    await Task.Delay(300);
                    await SendCommand(EVT_CDU_R_R5, 1);
                    await Task.Delay(300);
                    await SendCommand(EVT_CDU_R_R1, 1);
                    await Task.Delay(300);
                    await SendCommand(EVT_CDU_R_R6, 1);
                }
                catch (Exception ex)
                {
                    Logger.Error($"FMC Chocks sequence failed: {ex}");
                }
            });
        }

        public void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwRequestID == (uint)DATA_REQUESTS.PmdgVar)
            {
                var pmdgVars = (PmdgVarsStruct)data.dwData[0];
                UpdatePmdgVars(pmdgVars);
            }
        }

        private void UpdatePmdgVars(PmdgVarsStruct pmdgVars)
        {
            FwdLeftCabinDoor = pmdgVars.FwdLeftCabinDoor;
            FwdLeftCabinDoorFlag = pmdgVars.FwdLeftCabinDoorFlag;
            AftLeftCabinDoor = pmdgVars.AftLeftCabinDoor;
            AftLeftCabinDoorFlag = pmdgVars.AftLeftCabinDoorFlag;
            FwdRightCabinDoor = pmdgVars.FwdRightCabinDoor;
            FwdRightCabinDoorFlag = pmdgVars.FwdRightCabinDoorFlag;
            AftRightCabinDoor = pmdgVars.AftRightCabinDoor;
            AftRightCabinDoorFlag = pmdgVars.AftRightCabinDoorFlag;
            FwdLwrCargoDoor = pmdgVars.FwdLwrCargoDoor;
            AftLwrCargoDoor = pmdgVars.AftLwrCargoDoor;
            MainCargoDoor = pmdgVars.MainCargoDoor;
            EquipmentHatchDoor = pmdgVars.EEDoor;
            GroundPowerLightConnected = pmdgVars.GroundPowerLightConnected;
            Chocks = pmdgVars.Chocks;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PMDG_NG3_Control
    {
        public uint Event;
        public uint Parameter;
    }
}
