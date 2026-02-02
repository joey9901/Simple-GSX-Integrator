# Simple GSX Integrator

Automated GSX integration for Microsoft Flight Simulator 2024/2020. This tool automatically triggers GSX services based on your aircraft state, eliminating the need for manual menu navigation.

## Features
- **Automatic Service Triggering**
  - Optional Refueling before Boarding (configurable per aircraft, saved in config)
  - Boarding Requested upon manual Key Press
  - Pushback Request when Beacon light is turned ON
  - Deboarding when beacon light turns off at arrival
- **Hotkey Controls**
  - `ALT+G` - Toggle system ON/OFF (Calls Refueling or Boarding if conditions are met)
  - `ALT+B` - Reset session (for turnaround flights)
  - `ALT+R` - Toggle Refuel before Boarding for current aircraft

## Limitations
 - Refueling does not work if the selected aircraft does not support GSX Refueling. 

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
3. Press `ALT+G` when ready for Refueling or Boarding
4. Fly normally - GSX services trigger automatically based on your actions
5. Upon arrival - Select GSX gate

The app runs in the background and monitors your aircraft state to trigger appropriate GSX services at the right times.

## Configuration

Config file is located in the installation folder under `config/simplegsx.ini`:

Default hotkey configuration:
```ini
[Hotkeys]
ActivationKey=ALT+G
ResetKey=ALT+B
ToggleRefuelKey=ALT+R

[Aircraft:737-800 PAX BW HD]
RefuelBeforeBoarding=false
```

Refuel before Boarding option is also saved here for every aircraft

## Uninstallation

Run the installer again and click "Uninstall Existing Installation" on the welcome screen. This will:
- Remove all installed files
- Restore your original `exe.xml` file
- Clean up configuration files

## Requirements

- Microsoft Flight Simulator 2024 or 2020
- GSX Pro (FSDreamTeam)
- Windows 10/11

## How It Works

The app connects to MSFS via SimConnect and monitors:
- Beacon light state
- Parking brake state
- Engine state
- Ground speed
- SimConnect variables

When specific conditions are met (e.g., beacon turns off after landing), it sends commands to GSX to trigger the appropriate service.

## Troubleshooting

**App doesn't start with MSFS:**
- Check that `exe.xml` exists in your MSFS config folder
- Verify the path in `exe.xml` points to the correct installation location

**Services don't trigger:**
- Make sure GSX is running
- Press `ALT+G` to verify the system is activated
- Check the console window for status messages

**App won't close:**
- The app automatically closes when MSFS exits
- If it doesn't, you can close the console window manually

## License

MIT License - See LICENSE file for details

## Credits

Built for the flight simulation community. Uses SimConnect SDK from Microsoft Flight Simulator.
