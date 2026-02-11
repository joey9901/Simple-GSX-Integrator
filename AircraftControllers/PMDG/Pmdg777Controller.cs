using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimpleGsxIntegrator
{
    public sealed class Pmdg777Controller : AircraftControllerBase
    {
        private static bool _printedPmdg777Detected = false;

        private enum DATA_ID : uint
        {
            DATA = 0x504D4447,
            CONTROL = 0x504D4449
        }

        private enum DEF_ID : uint
        {
            DATA = 0x504D4448,
            CONTROL = 0x504D444A
        }

        private enum REQ_ID : uint
        {
            DATA = 1,
            CONTROL = 2
        }

        private const uint BASE = 0x00011000;

        private const uint EVT_DOOR_1L = BASE + 14011;
        private const uint EVT_DOOR_1R = BASE + 14012;
        private const uint EVT_DOOR_2L = BASE + 14013;
        private const uint EVT_DOOR_2R = BASE + 14014;
        private const uint EVT_DOOR_3L = BASE + 14015;
        private const uint EVT_DOOR_3R = BASE + 14016;
        private const uint EVT_DOOR_4L = BASE + 14017;
        private const uint EVT_DOOR_4R = BASE + 14018;
        private const uint EVT_DOOR_5L = BASE + 14019;
        private const uint EVT_DOOR_5R = BASE + 14020;
        private const uint EVT_DOOR_CARGO_FWD = BASE + 14021;
        private const uint EVT_DOOR_CARGO_AFT = BASE + 14022;
        private const uint EVT_DOOR_CARGO_BULK = BASE + 14023;
        private const uint EVT_DOOR_CARGO_MAIN = BASE + 14024;
        private const uint EVT_DOOR_FWD_ACCESS = BASE + 14025;
        private const uint EVT_DOOR_EE_ACCESS = BASE + 14026;

        private const uint EVT_CDU_L_L1 = BASE + 328;
        private const uint EVT_CDU_L_L2 = BASE + 329;
        private const uint EVT_CDU_L_L3 = BASE + 330;
        private const uint EVT_CDU_L_L4 = BASE + 331;
        private const uint EVT_CDU_L_L5 = BASE + 332;
        private const uint EVT_CDU_L_L6 = BASE + 333;
        private const uint EVT_CDU_L_R1 = BASE + 334;
        private const uint EVT_CDU_L_R2 = BASE + 335;
        private const uint EVT_CDU_L_R3 = BASE + 336;
        private const uint EVT_CDU_L_R4 = BASE + 337;
        private const uint EVT_CDU_L_R5 = BASE + 338;
        private const uint EVT_CDU_L_R6 = BASE + 339;
        private const uint EVT_CDU_L_MENU = BASE + 350;

        private const uint CDU_EVT_OFFSET_R = (BASE + 401) - EVT_CDU_L_L1;

        private const uint EVT_CDU_R_L1 = CDU_EVT_OFFSET_R + EVT_CDU_L_L1;
        private const uint EVT_CDU_R_L2 = CDU_EVT_OFFSET_R + EVT_CDU_L_L2;
        private const uint EVT_CDU_R_L3 = CDU_EVT_OFFSET_R + EVT_CDU_L_L3;
        private const uint EVT_CDU_R_L4 = CDU_EVT_OFFSET_R + EVT_CDU_L_L4;
        private const uint EVT_CDU_R_L5 = CDU_EVT_OFFSET_R + EVT_CDU_L_L5;
        private const uint EVT_CDU_R_L6 = CDU_EVT_OFFSET_R + EVT_CDU_L_L6;
        private const uint EVT_CDU_R_R1 = CDU_EVT_OFFSET_R + EVT_CDU_L_R1;
        private const uint EVT_CDU_R_R2 = CDU_EVT_OFFSET_R + EVT_CDU_L_R2;
        private const uint EVT_CDU_R_R3 = CDU_EVT_OFFSET_R + EVT_CDU_L_R3;
        private const uint EVT_CDU_R_R4 = CDU_EVT_OFFSET_R + EVT_CDU_L_R4;
        private const uint EVT_CDU_R_R5 = CDU_EVT_OFFSET_R + EVT_CDU_L_R5;
        private const uint EVT_CDU_R_R6 = CDU_EVT_OFFSET_R + EVT_CDU_L_R6;
        private const uint EVT_CDU_R_MENU = CDU_EVT_OFFSET_R + EVT_CDU_L_MENU;

        private const uint EVT_OH_ELEC_GRD_PWR_PRIM_SWITCH = BASE + 8;
        private const uint EVT_OH_ELEC_GRD_PWR_SEC_SWITCH = BASE + 7;

        public Pmdg777Controller(SimConnect sim, SimVarMonitor? simVarMonitor = null)
            : base(sim, simVarMonitor) { }

        public static bool IsPmdg777(string aircraftPath)
        {
            if (string.IsNullOrEmpty(aircraftPath)) return false;
            bool isPmdg777 = aircraftPath.Contains("PMDG 777", StringComparison.OrdinalIgnoreCase);

            if (isPmdg777 && !_printedPmdg777Detected)
            {
                Logger.Info("PMDG 777 Detected!");
                _printedPmdg777Detected = true;
            }
            else if (!isPmdg777)
            {
                _printedPmdg777Detected = false;
            }

            return isPmdg777;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Pmdg777VarsStruct
        {
            public double CabinDoor1L;
            public double CabinDoor1R;
            public double CabinDoor2L;
            public double CabinDoor2R;
            public double CabinDoor3L;
            public double CabinDoor3R;
            public double CabinDoor4L;
            public double CabinDoor4R;
            public double CabinDoor5L;
            public double CabinDoor5R;
            public double FwdLwrCargoDoor;
            public double AftLwrCargoDoor;
            public double MainCargoDoor;
            public double AvionicsAccessDoor;
            public double BulkCargoDoor;
            public double EquipmentHatchDoor;
            public double Chocks;
            public double SecondaryExternalPower;
            public double PrimaryExternalPower;
        }

        private Pmdg777VarsStruct _pmdg777Vars;

        public override void Connect()
        {
            try
            {
                Logger.Debug("PMDG 777: Initializing SDK");

                _simConnect.MapClientDataNameToID("PMDG_777X_Control", DATA_ID.CONTROL);

                uint sizeControl = (uint)Marshal.SizeOf<PMDG_777X_Control>();
                _simConnect.AddToClientDataDefinition(DEF_ID.CONTROL, 0, sizeControl, 0, 0);
                _simConnect.RegisterDataDefineStruct<PMDG_777X_Control>(DEF_ID.CONTROL);

                _simConnect.RequestClientData(DATA_ID.CONTROL, REQ_ID.CONTROL, DEF_ID.CONTROL,
                    SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

                try
                {
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor1L", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor1R", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor2L", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor2R", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor3L", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                       "L:7X7XCabinDoor3R", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                       "L:7X7XCabinDoor4L", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                       "L:7X7XCabinDoor4R", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XCabinDoor5L", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                       "L:7X7XCabinDoor5R", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XforwardcargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XaftcargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XmaincargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XavionicsDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XbulkcargoDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7XEEDoor", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:7X7X_WheelChocks", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:switch_07_b", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    _simConnect.AddToDataDefinition(DEFINITIONS.PmdgVar777,
                        "L:switch_08_b", "Number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                    _simConnect.RegisterDataDefineStruct<Pmdg777VarsStruct>(DEFINITIONS.PmdgVar777);

                    // Simconnect seems to have an issue where it stops sending packets, this seems to be a working workaround...
                    _simConnect.RequestDataOnSimObject(
                        DATA_REQUESTS.PmdgVar777,
                        DEFINITIONS.PmdgVar777,
                        SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        SIMCONNECT_PERIOD.SIM_FRAME,
                        SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                        0, 0, 0);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        _simConnect.RequestDataOnSimObject(
                            DATA_REQUESTS.PmdgVar777,
                            DEFINITIONS.PmdgVar777,
                            SimConnect.SIMCONNECT_OBJECT_ID_USER,
                            SIMCONNECT_PERIOD.SECOND,
                            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                            0, 0, 0);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"PMDG var registration failed: {ex.Message}");
                }

                Logger.Debug("PMDG SDK connected!");
            }
            catch (Exception ex)
            {
                Logger.Error($"PMDG SDK init failed: {ex}");
            }
        }

        public override void CloseOpenDoors()
        {
            try
            {
                var doors = new[] { EVT_DOOR_1L, EVT_DOOR_1R, EVT_DOOR_2L, EVT_DOOR_2R, EVT_DOOR_3L, EVT_DOOR_3R,
                    EVT_DOOR_4L, EVT_DOOR_4R, EVT_DOOR_5L, EVT_DOOR_5R, EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT,
                    EVT_DOOR_CARGO_MAIN, EVT_DOOR_CARGO_BULK, EVT_DOOR_FWD_ACCESS, EVT_DOOR_EE_ACCESS };
                bool doorsToClose = false;

                foreach (var door in doors)
                {
                    if (IsDoorOpen(door))
                    {
                        doorsToClose = true;
                        Close(door);
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
        }

        private bool Close(uint door)
        {
            if (!IsDoorOpen(door))
            {
                Logger.Debug($"PMDG: door '{GetDoorName(door)}' already closed");
                return true;
            }

            return SendCommand(door, 1);
        }

        private bool IsDoorOpen(uint door)
        {
            double val = double.NaN;

            switch (door)
            {
                case EVT_DOOR_1L:
                    val = _pmdg777Vars.CabinDoor1L;
                    break;
                case EVT_DOOR_1R:
                    val = _pmdg777Vars.CabinDoor1R;
                    break;
                case EVT_DOOR_2L:
                    val = _pmdg777Vars.CabinDoor2L;
                    break;
                case EVT_DOOR_2R:
                    val = _pmdg777Vars.CabinDoor2R;
                    break;
                case EVT_DOOR_3L:
                    val = _pmdg777Vars.CabinDoor3L;
                    break;
                case EVT_DOOR_3R:
                    val = _pmdg777Vars.CabinDoor3R;
                    break;
                case EVT_DOOR_4L:
                    val = _pmdg777Vars.CabinDoor4L;
                    break;
                case EVT_DOOR_4R:
                    val = _pmdg777Vars.CabinDoor4R;
                    break;
                case EVT_DOOR_5L:
                    val = _pmdg777Vars.CabinDoor5L;
                    break;
                case EVT_DOOR_5R:
                    val = _pmdg777Vars.CabinDoor5R;
                    break;
                case EVT_DOOR_CARGO_FWD:
                    val = _pmdg777Vars.FwdLwrCargoDoor;
                    break;
                case EVT_DOOR_CARGO_AFT:
                    val = _pmdg777Vars.AftLwrCargoDoor;
                    break;
                case EVT_DOOR_CARGO_MAIN:
                    val = _pmdg777Vars.MainCargoDoor;
                    break;
                case EVT_DOOR_CARGO_BULK:
                    val = _pmdg777Vars.BulkCargoDoor;
                    break;
                case EVT_DOOR_FWD_ACCESS:
                    val = _pmdg777Vars.AvionicsAccessDoor;
                    break;
                case EVT_DOOR_EE_ACCESS:
                    val = _pmdg777Vars.EquipmentHatchDoor;
                    break;
                default:
                    return false;
            }

            if (double.IsNaN(val)) return false;

            return val >= 0.50;
        }

        public override bool AreAnyDoorsOpen()
        {
            var doors = new[] { EVT_DOOR_1L, EVT_DOOR_1R, EVT_DOOR_2L, EVT_DOOR_2R, EVT_DOOR_3L, EVT_DOOR_3R,
                    EVT_DOOR_4L, EVT_DOOR_4R, EVT_DOOR_5L, EVT_DOOR_5R, EVT_DOOR_CARGO_FWD, EVT_DOOR_CARGO_AFT,
                    EVT_DOOR_CARGO_MAIN, EVT_DOOR_CARGO_BULK, EVT_DOOR_FWD_ACCESS, EVT_DOOR_EE_ACCESS };

            foreach (var door in doors)
            {
                if (IsDoorOpen(door))
                {
                    return true;
                }
            }

            return false;
        }

        private bool SendCommand(uint evt, uint param)
        {
            var cmd = new PMDG_777X_Control { Event = evt, Parameter = param };
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
                EVT_DOOR_1L => "Cabin 1L",
                EVT_DOOR_1R => "Cabin 1R",
                EVT_DOOR_2L => "Cabin 2L",
                EVT_DOOR_2R => "Cabin 2R",
                EVT_DOOR_3L => "Cabin 3L",
                EVT_DOOR_3R => "Cabin 3R",
                EVT_DOOR_4L => "Cabin 4L",
                EVT_DOOR_4R => "Cabin 4R",
                EVT_DOOR_5L => "Cabin 5L",
                EVT_DOOR_5R => "Cabin 5R",
                EVT_DOOR_CARGO_FWD => "Forward Cargo",
                EVT_DOOR_CARGO_AFT => "Aft Cargo",
                EVT_DOOR_CARGO_MAIN => "Main Cargo",
                EVT_DOOR_FWD_ACCESS => "Avionics Access",
                EVT_DOOR_CARGO_BULK => "Bulk Cargo",
                _ => $"evt_{evt}"
            };
        }

        public override void RemoveGroundEquipment()
        {
            try
            {
                if (_pmdg777Vars.SecondaryExternalPower != 0)
                {
                    SendCommand(EVT_OH_ELEC_GRD_PWR_SEC_SWITCH, 1);
                }

                if (_pmdg777Vars.PrimaryExternalPower != 0)
                {
                    SendCommand(EVT_OH_ELEC_GRD_PWR_PRIM_SWITCH, 1);
                }

                bool chocksSet = _pmdg777Vars.Chocks > 0.5;

                if (!chocksSet)
                {
                    Logger.Debug("Chocks not set, skipping FMC sequence");
                    return;
                }

                Logger.Info("Removing Chocks");

                SendCommand(EVT_CDU_R_MENU, 1);
                Task.Delay(300).Wait();
                SendCommand(EVT_CDU_R_R6, 1);
                Task.Delay(300).Wait();
                SendCommand(EVT_CDU_R_R1, 1);
                Task.Delay(300).Wait();
                SendCommand(EVT_CDU_R_R6, 1);
            }
            catch (Exception ex)
            {
                Logger.Error($"FMC Chocks sequence failed: {ex}");
            }
        }

        public override void OnSimObjectDataReceived(SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwDefineID != (uint)DEFINITIONS.PmdgVar777 && data.dwRequestID != (uint)DATA_REQUESTS.PmdgVar777)
                return;

            try
            {
                _pmdg777Vars = (Pmdg777VarsStruct)data.dwData[0];
            }
            catch (Exception ex)
            {
                Logger.Error($"Parse failed: {ex}");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PMDG_777X_Control
    {
        public uint Event;
        public uint Parameter;
    }
}
