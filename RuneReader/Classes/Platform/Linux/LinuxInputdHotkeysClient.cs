#nullable enable
using System;
using System.Threading.Tasks;

namespace RuneReader.Classes.Platform.Linux;

public sealed class RunereaderInputdHotkeysClient : IGlobalHotkeys
{
    private readonly InputdConnection _conn;
    private readonly string _activationKey;

    private bool _started;

    public event Action<HotkeyActionResult>? ActivateKeyChanged;
    public event Action<HotkeyActionResult>? CtrlChanged;
    public event Action<HotkeyActionResult>? AltChanged;
    public event Action<HotkeyActionResult>? ShiftChanged;

    public event Action<HotkeyActionResult>? ActivateKeyChangedAsync;
    public event Action<HotkeyActionResult>? CtrlChangedAsync;
    public event Action<HotkeyActionResult>? AltChangedAsync;
    public event Action<HotkeyActionResult>? ShiftChangedAsync;

    public RunereaderInputdHotkeysClient(string activationKey, string socketPath, string sharedKey)
    {
        _activationKey = activationKey;
        _conn = new InputdConnection(socketPath, sharedKey);
        _conn.LineReceived += OnLine;
    }

    public bool isStarted() => _started;

    public void Start()
    {
        if (_started) return;

        _conn.Connect();

        // configure activation key inside daemon
        _conn.SendAndReadLine($"SET_ACTKEY {_activationKey}", expectOkPrefix: "OK SET_ACTKEY");

        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;

        _conn.Dispose();
        _started = false;
    }

    public void Dispose() => Stop();

    private void OnLine(string line)
    {
        // Server messages:
        // ACT DOWN|UP
        // MOD CTRL|ALT|SHIFT DOWN|UP
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        if (parts[0] == "ACT" && parts.Length >= 2)
        {
            var state = parts[1] == "DOWN" ? HotkeyState.PRESSED : HotkeyState.RELEASED;
            Fire(ActivateKeyChanged, ActivateKeyChangedAsync, state);
            return;
        }

        if (parts[0] == "MOD" && parts.Length >= 3)
        {
            var mod = parts[1].ToUpperInvariant();
            var state = parts[2] == "DOWN" ? HotkeyState.PRESSED : HotkeyState.RELEASED;

            switch (mod)
            {
                case "CTRL":
                    Fire(CtrlChanged, CtrlChangedAsync, state);
                    break;
                case "ALT":
                    Fire(AltChanged, AltChangedAsync, state);
                    break;
                case "SHIFT":
                    Fire(ShiftChanged, ShiftChangedAsync, state);
                    break;
            }
        }
    }

    private static void Fire(Action<HotkeyActionResult>? sync,
                             Action<HotkeyActionResult>? async,
                             HotkeyState state)
    {
        var r = new HotkeyActionResult(state);
        sync?.Invoke(r);
        if (async != null) _ = Task.Run(() => async(r));
    }
}
