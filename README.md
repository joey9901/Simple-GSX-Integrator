# Simple GSX Integrator

Automated GSX Integration for Microsoft Flight Simulator 2024/2020. This Tool Automatically Triggers GSX Services based on your Aircraft State, Eliminating the need for Manual Menu Navigation.

## Features
- **Automatic Service Triggering**
  - Optional Automatic Catering Request before Boarding
  - Optional Automatic Refueling Request before Boarding
  - Automatic Boarding Request upon Singular Manual Key Press Activation
  - Automatic Deboarding Request when Beacon Light turns OFF and Parking Brake is SET
  - `ALT+G` - Toggle system ON/OFF (Calls Services if Conditions are met)
  - `ALT+B` - Reset Session (Resets internal states if something breaks) 
  - Automatic Door Closing for PMDG 737 after Boarding is Completed (WIP)
  - Custom L:Var Trigger for System Activation

## Limitations
 - This App only calls GSX Services, any issues with GSX (Refueling not working, doors not closing, etc.) are NOT part of this Project
 - Due to SimVar Limitations, Handling Operator choice is limited to the first option

## Installation

1. Download `SimpleGSXIntegrator-Installer.exe` from the [Releases](../../releases) page
2. Run the installer
3. Click Next, confirm installation location
4. Click Install

The installer will:
- Detect your MSFS installation automatically
- Install the application
- Configure auto-launch with MSFS via `exe.xml`

## Usage
**Important: Keep GSX Menu Hidden**
1. Launch MSFS - the app starts automatically
2. Load your flight and aircraft
3. Press `ALT+G` (default key) when ready for Catering, Refueling or Boarding
4. Fly normally - GSX services trigger automatically based on your actions
5. Upon arrival - Select GSX gate

The app runs in the background and monitors your aircraft state to trigger appropriate GSX services at the right times.

## Configuration

Config file is located in the installation folder under `config/simplegsx.ini`:

Default hotkey configuration (can be changed in UI):
```ini
[Hotkeys]
ActivationKey=ALT+G
ResetKey=ALT+B

[Aircraft:737-800 PAX BW HD]
RefuelBeforeBoarding=false
...
```

## Uninstallation

Run the installer again and click "Uninstall Existing Installation" on the welcome screen. This will:
- Remove all installed files
- Remove Simple GSX Integrator entry from `exe.xml` file
- Clean up configuration files (if selected)

## Requirements

- Microsoft Flight Simulator 2024 or 2020
- GSX Pro (FSDreamTeam)
- Windows 10/11

## How It Works

The app connects to MSFS via SimConnect and monitors variables like:
- Beacon Light State
- Parking Brake State
- Engine State
- Ground Speed
- GSX Variables

When specific conditions are met (e.g., Beacon Light turns OFF after landing), it sends commands to GSX to trigger the appropriate service (deboarding).

## Troubleshooting

**App doesn't start with MSFS:**
- Check that `exe.xml` exists in your MSFS config folder
- Verify the path in `exe.xml` points to the correct installation location

**Services don't trigger:**
- Make sure GSX is running
- Press `ALT+G` to verify the system is activated
- Check the console window for status messages

## License
MIT License - See LICENSE file for details