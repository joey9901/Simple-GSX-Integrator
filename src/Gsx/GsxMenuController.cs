using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using SimpleGsxIntegrator.Config;
using SimpleGsxIntegrator.Core;

namespace SimpleGsxIntegrator.Gsx;

/// <summary>
/// Sends commands to GSX by writing to the menu L:vars via SimConnect.
///
/// GSX menu sequence:
///   1. Write 1 → L:FSDT_GSX_MENU_OPEN  (open menu)
///   2. Write (item – 1) → L:FSDT_GSX_MENU_CHOICE  (select item)
///   3. Write 1 → L:FSDT_GSX_MENU_CHOICE  (confirm operator / first option)
///   4. Write 0 → L:FSDT_GSX_MENU_OPEN  (close menu)
///
/// All methods are async-safe and use async delays instead of Thread.Sleep.
/// </summary>
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

    /// <summary>Wire this to <see cref="SimConnectHub.Connected"/>.</summary>
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

    public Task CallBoardingAsync() => CallServiceAsync("Boarding", MenuItemBoarding);
    public Task CallCateringAsync() => CallServiceAsync("Catering", MenuItemCatering);
    public Task CallRefuelingAsync() => CallServiceAsync("Refueling", MenuItemRefueling);
    public Task CallPushbackAsync() => CallServiceAsync("Pushback", MenuItemPushback, closeAfter: false);
    public Task CallDeboardingAsync() => CallDeboardingSequenceAsync();

    private async Task CallServiceAsync(string name, int menuItem, bool closeAfter = true)
    {
        if (_sc == null)
        {
            Logger.Warning($"GsxMenuController: cannot call {name} – not connected");
            return;
        }

        Logger.Info($"GSX: Calling {name}");

        try
        {
            await CloseMenuAsync();
            await Task.Delay(MenuOpenDelayMs);

            await OpenMenuAsync();
            await Task.Delay(MenuOpenDelayMs);

            await SelectItemAsync(menuItem);
            await Task.Delay(MenuChoiceDelayMs);

            AutoSelectOperator();
            await Task.Delay(MenuChoiceDelayMs);

            if (closeAfter)
            {
                await CloseMenuAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"GsxMenuController: {name} sequence failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Deboarding uses a slightly different sequence: open → item 1 (deboard) → sub-item 2 (confirm).
    /// </summary>
    private async Task CallDeboardingSequenceAsync()
    {
        if (_sc == null) return;

        Logger.Info("GSX: Calling Deboarding");

        try
        {
            await CloseMenuAsync();
            await Task.Delay(MenuOpenDelayMs);

            await OpenMenuAsync();
            await Task.Delay(MenuOpenDelayMs);

            await SelectItemAsync(MenuItemDeboarding);
            await Task.Delay(MenuChoiceDelayMs);

            // Sub-menu: select option 2 (proceed with deboarding at current gate)
            await SelectItemAsync(2);
            await Task.Delay(MenuCloseDelayMs);

            await CloseMenuAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"GsxMenuController: Deboarding sequence failed: {ex.Message}");
        }
    }


    private Task OpenMenuAsync()
    {
        WriteVar(SimDef.GsxMenuOpen, 1.0);
        return Task.CompletedTask;
    }

    private Task CloseMenuAsync()
    {
        WriteVar(SimDef.GsxMenuOpen, 0.0);
        return Task.CompletedTask;
    }

    private Task SelectItemAsync(int item)
    {
        // GSX expects 0-based index
        WriteVar(SimDef.GsxMenuChoice, (double)(item - 1));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Selects the first operator (index 0) once, then marks it done for this session
    /// so we don't spam operator selection on subsequent service calls.
    /// </summary>
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

    /// <summary>Resets the operator-selection guard (e.g. on GSX restart).</summary>
    public void ResetOperatorSelection() => _operatorAutoSelected = false;
}
