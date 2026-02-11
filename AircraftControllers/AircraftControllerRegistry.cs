using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;

namespace SimpleGsxIntegrator
{
    public static class AircraftControllerRegistry
    {
        private static readonly List<(Func<string, bool> matcher, Func<SimConnect, SimVarMonitor?, IAircraftController> factory)> AircraftList
            = new List<(Func<string, bool>, Func<SimConnect, SimVarMonitor?, IAircraftController>)>();

        public static void Register(Func<string, bool> matcher, Func<SimConnect, SimVarMonitor?, IAircraftController> factory)
        {
            AircraftList.Add((matcher, factory));
        }

        public static void RegisterDefaults()
        {
            Register(path => !string.IsNullOrEmpty(path) && path.Contains("PMDG 737", StringComparison.OrdinalIgnoreCase),
                (sim, vars) => new Pmdg737Controller(sim, vars));

            Register(path => !string.IsNullOrEmpty(path) && path.Contains("PMDG 777", StringComparison.OrdinalIgnoreCase),
                (sim, vars) => new Pmdg777Controller(sim, vars));
        }

        public static IAircraftController? CreateAircraftController(string aircraftPath, SimConnect simConnect, SimVarMonitor? simVarMonitor)
        {
            if (AircraftList.Count == 0)
            {
                Logger.Debug("AircraftControllerRegistry: No controllers registered.");
                return null;
            }

            Logger.Debug($"AircraftControllerRegistry: CreateAircraftController called for '{aircraftPath}'");

            foreach (var aircraft in AircraftList)
            {
                if (aircraft.matcher(aircraftPath))
                {
                    var controller = aircraft.factory(simConnect, simVarMonitor);
                    Logger.Debug($"AircraftControllerRegistry: matched controller '{controller.GetType().Name}' for '{aircraftPath}'");
                    return controller;
                }
            }

            Logger.Debug($"AircraftControllerRegistry: no controller matched for '{aircraftPath}'");
            return null;
        }
    }
}
