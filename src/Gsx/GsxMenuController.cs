using System.Runtime.InteropServices;
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
    private bool _operatorAutoSelected;

    private const int MenuItemDeboarding = 1;
    private const int MenuItemCatering = 2;
    private const int MenuItemRefueling = 3;
    private const int MenuItemBoarding = 4;
    private const int MenuItemPushback = 5;
    private const int DeboardingConfirmItem = 2;

    public void OnSimConnectConnected(SimConnect sc)
    {
        _sc = sc;
        _operatorAutoSelected = false;

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
        return CallServiceAsync("Pushback", MenuItemPushback, closeAfter: false);
    }

    public Task CallDeboardingAsync()
    {
        return CallDeboardingSequenceAsync();
    }

    private async Task CallServiceAsync(string name, int menuItem, bool closeAfter = true)
    {
        if (_sc == null)
        {
            Logger.Warning($"GsxMenuController: cannot call {name} – not connected");
            return;
        }

        Logger.Info($"GSX: Calling {name}");

        CloseMenu();
        await Task.Delay(MenuOpenDelayMs);

        OpenMenu();
        await Task.Delay(MenuOpenDelayMs);

        SelectItem(menuItem);
        await Task.Delay(MenuChoiceDelayMs);

        AutoSelectOperator();
        await Task.Delay(MenuChoiceDelayMs);

        if (closeAfter)
            CloseMenu();
    }
    private async Task CallDeboardingSequenceAsync()
    {
        if (_sc == null) return;

        Logger.Info("GSX: Calling Deboarding");

        CloseMenu();
        await Task.Delay(MenuOpenDelayMs);

        OpenMenu();
        await Task.Delay(MenuOpenDelayMs);

        SelectItem(MenuItemDeboarding);
        await Task.Delay(MenuChoiceDelayMs);

        SelectItem(DeboardingConfirmItem);
        await Task.Delay(MenuCloseDelayMs);

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

    private void AutoSelectOperator()
    {
        if (_operatorAutoSelected) return;
        WriteVar(SimDef.GsxMenuChoice, 0.0);
        _operatorAutoSelected = true;
        Logger.Debug("GsxMenuController: auto-selected operator (index 0)");
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

    public void ResetOperatorSelection()
    {
        _operatorAutoSelected = false;
    }
}
