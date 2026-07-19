using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Config;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Gsx;

public sealed class GsxMenuController
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct GsxVarSetStruct { public double Value; }

    private const int MenuOpenDelayMs = 1500;
    private const int MenuChoiceDelayMs = 1200;
    private const int MenuCloseDelayMs = 800;

    private SimConnect? _sc;
    private string? _gsxMenuFilePath;
    private string _liveryName = string.Empty;

    public void SetLiveryName(string liveryName)
    {
        _liveryName = liveryName;
    }

    public GsxMenuController()
    {
        _gsxMenuFilePath = FindGsxMenuFilePath();
        if (_gsxMenuFilePath != null)
            Logger.Debug($"GsxMenuController: GSX menu file found at {_gsxMenuFilePath}");
        else
            Logger.Debug("GsxMenuController: GSX menu file not found - will default to first operator");
    }

    [SupportedOSPlatform("windows")]
    private static string? FindGsxMenuFilePath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\FSDreamTeam");
            var root = key?.GetValue("root") as string;
            if (string.IsNullOrEmpty(root)) return null;
            var path = Path.Combine(root, "MSFS", "fsdreamteam-gsx-pro",
                "html_ui", "InGamePanels", "FSDT_GSX_Panel", "menu");
            return File.Exists(path) ? path : null;
        }
        catch { return null; }
    }

    private const int MenuItemDeboarding = 1;
    private const int MenuItemCatering = 2;
    private const int MenuItemRefueling = 3;
    private const int MenuItemBoarding = 4;
    private const int MenuItemPushback = 5;
    private const int DeboardingConfirmItem = 2;

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;

        sc.AddToDataDefinition(SimDef.GsxMenuOpen, GsxConstants.MenuOpen, null,
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GsxVarSetStruct>(SimDef.GsxMenuOpen);

        sc.AddToDataDefinition(SimDef.GsxMenuChoice, GsxConstants.MenuChoice, null,
            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        sc.RegisterDataDefineStruct<GsxVarSetStruct>(SimDef.GsxMenuChoice);

        Logger.Debug("GsxMenuController: SimConnect vars registered");
    }

    public Task CallBoardingAsync()
    {
        return CallServiceAsync("Boarding", MenuItemBoarding);
    }

    public Task CallCateringAsync()
    {
        return CallServiceAsync("Catering", MenuItemCatering);
    }

    public Task CallRefuelingAsync()
    {
        return CallServiceAsync("Refueling", MenuItemRefueling);
    }

    public Task CallPushbackAsync()
    {
        return CallServiceAsync("Push-back", MenuItemPushback, closeAfter: false);
    }

    public Task CallDeboardingAsync()
    {
        return CallServiceAsync("Deboarding", MenuItemDeboarding);
    }

    /// Opens and closes the GSX menu briefly - used as a state-update trigger for certain aircraft systems
    /// (e.g., FSLabs GPU state) that react to menu state changes without needing a menu interaction.
    public async Task FlashMenuAsync()
    {
        if (_sc == null) return;
        OpenMenu();
        await Task.Delay(300);
        CloseMenu();
    }

    private async Task CallServiceAsync(string name, int menuItem, bool closeAfter = true)
    {
        if (_sc == null)
        {
            Logger.Warning($"GsxMenuController: cannot call {name} - not connected");
            return;
        }

        Logger.Info($"GSX: Calling {name}");

        CloseMenu();
        await Task.Delay(MenuOpenDelayMs);

        OpenMenu();
        await Task.Delay(MenuOpenDelayMs);

        bool menuItemFound = false;
        var menuItems = ReadMenuFileLines();
        foreach (var item in menuItems)
        {
            if (item.Contains(name, StringComparison.OrdinalIgnoreCase))
                menuItemFound = true;
            Logger.Debug($"GSX menu item: {item}");
        }

        if (!menuItemFound)
        {
            Logger.Debug($"GSX menu item not found {name}");
            return;
        }

        SelectItem(menuItem);
        await Task.Delay(MenuChoiceDelayMs);

        AutoSelectOperator(_liveryName);
        await Task.Delay(MenuChoiceDelayMs);

        if (closeAfter)
            CloseMenu();
    }

    private void OpenMenu()
    {
        WriteVar(SimDef.GsxMenuOpen, 1.0);
    }

    private void CloseMenu()
    {
        WriteVar(SimDef.GsxMenuOpen, 0.0);
    }

    private void SelectItem(int item)
    {
        // GSX expects 0-based index
        WriteVar(SimDef.GsxMenuChoice, (double)(item - 1));
    }

    private void AutoSelectOperator(string liveryName)
    {
        var menuLines = ReadMenuFileLines();
        bool operatorSelection = false;
        foreach (var menuLine in menuLines)
        {
            Logger.Debug(menuLine);
            if (menuLine.Contains("operator", StringComparison.OrdinalIgnoreCase))
                operatorSelection = true;
        }

        if (!operatorSelection) return;

        Logger.Debug($"GSX: Selecting operator, livery='{liveryName}'");

        var operators = ReadMenuFileLines();
        if (operators.Count > 0)
        {
            Logger.Debug($"GSX operator menu ({operators.Count} option(s)):");
            for (int i = 1; i < operators.Count; i++)
                Logger.Debug($"  [{i}] {operators[i]}");

            if (!string.IsNullOrWhiteSpace(liveryName))
            {
                var words = liveryName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    for (int i = 1; i < operators.Count; i++)
                    {
                        if (operators[i].Contains(word, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Debug($"GSX: Selected operator '{operators[i].Trim()}' (matched livery word '{word}', index {i})");
                            WriteVar(SimDef.GsxMenuChoice, (double)i);
                            return;
                        }
                    }
                }
                Logger.Debug($"GSX: No operator matched any word in livery '{liveryName}'");
            }

            for (int i = 1; i < operators.Count; i++)
            {
                if (operators[i].Contains("swissport", StringComparison.OrdinalIgnoreCase))
                {
                    string name = operators[i].Replace("[GSX choice]", "").Trim();
                    Logger.Debug($"GSX: Selected operator '{name}' (Swissport fallback, index {i})");
                    WriteVar(SimDef.GsxMenuChoice, (double)i);
                    return;
                }
            }

            for (int i = 1; i < operators.Count; i++)
            {
                if (operators[i].Contains("[GSX choice]", StringComparison.OrdinalIgnoreCase))
                {
                    string name = operators[i].Replace("[GSX choice]", "").Trim();
                    Logger.Debug($"GSX: Selected operator '{name}' (GSX default, index {i})");
                    WriteVar(SimDef.GsxMenuChoice, (double)i);
                    return;
                }
            }
        }
        else
        {
            Logger.Debug("GSX operator menu: no options found in menu file");
        }

        Logger.Debug("GSX: Selecting operator at index 1 (fallback)");
        WriteVar(SimDef.GsxMenuChoice, 1.0);
    }

    private List<string> ReadMenuFileLines()
    {
        try
        {
            return File.ReadAllLines(_gsxMenuFilePath!)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }
        catch { return []; }
    }

    private void WriteVar(SimDef def, double value)
    {
        if (_sc == null) return;
        try
        {
            _sc.SetDataOnSimObject(def, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                new GsxVarSetStruct { Value = value });
        }
        catch (Exception ex)
        {
            Logger.Warning($"GsxMenuController: WriteVar({def}) failed: {ex.Message}");
        }
    }
}
