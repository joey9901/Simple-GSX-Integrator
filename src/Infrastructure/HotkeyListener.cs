using System.Runtime.InteropServices;
using SimpleGsxIntegrator.Config;

namespace SimpleGsxIntegrator.Infrastructure;

/// <summary>
/// Polls the Windows keyboard state at a fixed interval to detect configurable
/// hotkey combinations. Uses rising-edge detection (fires on key-up after key-down)
/// so a held key does not repeat-fire.
/// </summary>
public sealed class HotkeyListener : IDisposable
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_MENU = 0x12;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;

    private const int PollIntervalMs = 100;

    private ParsedHotkey? _activationHotkey;
    private ParsedHotkey? _resetHotkey;

    private bool _lastActivationState;
    private bool _lastResetState;

    public event Action? ActivationPressed;
    public event Action? ResetPressed;

    private CancellationTokenSource? _cts;
    private volatile bool _rebinding;
    private int _rebindCooldownTicks;

    public HotkeyListener() { }

    /// <summary>Convenience constructor that parses string hotkey names on creation.</summary>
    public HotkeyListener(string activationKey, string resetKey)
    {
        _activationHotkey = HotkeyParser.Parse(activationKey);
        _resetHotkey = HotkeyParser.Parse(resetKey);
    }

    public void UpdateHotkeys(ParsedHotkey activation, ParsedHotkey reset)
    {
        _activationHotkey = activation;
        _resetHotkey = reset;
    }

    /// <summary>Updates only the activation hotkey from a display string (e.g. "Ctrl+F1").</summary>
    public void SetActivationKey(string hotkeyString) => _activationHotkey = HotkeyParser.Parse(hotkeyString);

    /// <summary>Updates only the reset hotkey from a display string (e.g. "Ctrl+F2").</summary>
    public void SetResetKey(string hotkeyString) => _resetHotkey = HotkeyParser.Parse(hotkeyString);

    /// <summary>Starts the polling loop in a background task.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(PollLoopAsync, _cts.Token);
    }

    /// <summary>
    /// Temporarily suspends hotkey detection while the user is rebinding a key
    /// to prevent the new combo from immediately firing.
    /// </summary>
    public void SetRebinding(bool rebinding)
    {
        _rebinding = rebinding;
        if (!rebinding) _rebindCooldownTicks = 3;
    }

    public void Stop() => _cts?.Cancel();
    public void Dispose() => Stop();

    private async Task PollLoopAsync()
    {
        while (_cts?.IsCancellationRequested == false)
        {
            try
            {
                if (_rebinding || _rebindCooldownTicks > 0)
                {
                    _lastActivationState = false;
                    _lastResetState = false;
                    if (_rebindCooldownTicks > 0) _rebindCooldownTicks--;
                }
                else
                {
                    bool actNow = IsPressed(_activationHotkey);
                    bool resetNow = IsPressed(_resetHotkey);

                    // Rising-edge detection: fire on release (was pressed, now released)
                    if (_lastActivationState && !actNow) ActivationPressed?.Invoke();
                    if (_lastResetState && !resetNow) ResetPressed?.Invoke();

                    _lastActivationState = actNow;
                    _lastResetState = resetNow;
                }
            }
            catch { /* swallow poll errors */ }

            await Task.Delay(PollIntervalMs);
        }
    }

    private static bool IsPressed(ParsedHotkey? hotkey)
    {
        if (hotkey == null || hotkey.KeyCode == 0) return false;
        if ((GetAsyncKeyState(hotkey.KeyCode) & 0x8000) == 0) return false;
        if (hotkey.RequiresAlt && (GetAsyncKeyState(VK_MENU) & 0x8000) == 0) return false;
        if (hotkey.RequiresCtrl && (GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0) return false;
        if (hotkey.RequiresShift && (GetAsyncKeyState(VK_SHIFT) & 0x8000) == 0) return false;
        return true;
    }
}
