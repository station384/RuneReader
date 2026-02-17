#nullable enable
using System.Net.Sockets;
using System.Text;

namespace runereader_inputd;

internal sealed class InputDServer
{
    private readonly string _socketPath;
    private readonly string _sharedKey;
    private readonly UInputKeyboard _uinput;
    private readonly EvdevKeyboardMonitor _monitor;

    private readonly object _clientGate = new();
    private readonly List<ClientConnection> _clients = new();

    // Current activation keycode (defaults to "1")
    private ushort _activationKeyCode = KeyMaps.ActivationKeyMap["1"];

    public InputDServer(string socketPath, string sharedKey, UInputKeyboard uinput, EvdevKeyboardMonitor monitor)
    {
        _socketPath = socketPath;
        _sharedKey = sharedKey;
        _uinput = uinput;
        _monitor = monitor;

        _monitor.KeyEvent += OnMonitorKeyEvent;
    }

    
    public async Task RunAsync()
    {
        var ep = new UnixDomainSocketEndPoint(_socketPath);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(ep);

        // Permissions: 0660 on the socket file so your app group can connect.
        try
        {
            Sys.chmod(_socketPath, Convert.ToUInt32("660", 8));
        }
        catch { /* ignore */ }

        listener.Listen(backlog: 50);

        Console.WriteLine("Listening...");

        while (true)
        {
            var socket = await listener.AcceptAsync();
            _ = Task.Run(() => HandleClientAsync(socket));
        }
    }

    private void OnMonitorKeyEvent(object? sender, EvdevKeyboardMonitor.KeyEventArgs e)
    {
        // Activation key only
        if (e.Code == _activationKeyCode)
        {
            Broadcast($"ACT {(e.Pressed ? "DOWN" : "UP")}");
            Console.WriteLine($" ACT: {(e.Pressed ? "ON" : "OFF")}");
        }

        // Modifiers (optional but useful for parity with your IGlobalHotkeys)
        if (KeyMaps.ModifierCodeToName.TryGetValue(e.Code, out var mod))
        {
            Broadcast($"MOD {mod} {(e.Pressed ? "DOWN" : "UP")}");
            Console.WriteLine($" MOD: {(e.Pressed ? "DOWN" : "UP")}");

        }
    }

    private void Broadcast(string line)
    {
        List<ClientConnection> snapshot;
        lock (_clientGate) snapshot = _clients.ToList();

        foreach (var c in snapshot)
            c.TrySend(line);
    }

    private async Task HandleClientAsync(Socket socket)
    {
        var client = new ClientConnection(socket);

        lock (_clientGate) _clients.Add(client);

        Console.WriteLine("Client connected");

        try
        {
            client.TrySend("HELLO runereader-inputd 1");
            client.TrySend("AUTH_REQUIRED 1");

            bool authed = false;

            while (true)
            {
                var line = await client.ReadLineAsync();
                if (line == null) break;

                if (line.Length == 0) continue;

                var (cmd, rest) = SplitCmd(line);

                if (!authed)
                {
                    if (cmd == "AUTH")
                    {
                        if (ConstantTimeEquals(rest.Trim(), _sharedKey))
                        {
                            authed = true;
                            client.TrySend("OK AUTH");
                            Console.WriteLine($"AUTH: OK");
                        }
                        else
                        {
                            client.TrySend("ERR AUTH");
                            Console.WriteLine($"AUTH: ERR");
                            break;
                        }
                    }
                    else
                    {
                        client.TrySend("ERR NOT_AUTHED");
                        Console.WriteLine($"AUTH: ERR NOT_AUTHED");

                    }

                    continue;
                }

                switch (cmd)
                {
                    case "PING":
                        client.TrySend("PONG");
                        break;

                    case "SET_ACTKEY":
                        {
                            var tok = rest.Trim();
                            if (!KeyMaps.ActivationKeyMap.TryGetValue(tok, out var code))
                            {
                                client.TrySend("ERR SET_ACTKEY invalid_key");
                                break;
                            }
                            _activationKeyCode = code;
                            client.TrySend($"OK SET_ACTKEY {tok.ToUpperInvariant()}");
                            Console.WriteLine($"OK SET_ACTKEY {tok.ToUpperInvariant()}");
                            break;
                        }

                    case "INJECT":
                        {
                            if (!TryHandleInject(rest, client, out var err))
                            {
                                client.TrySend($"ERR INJECT {err}");
                                Console.WriteLine($"ERR INJECT {err}");

                            }
                            else
                            {
                                client.TrySend("OK INJECT");
                                Console.WriteLine($"OK INJECT");

                            }
                            break;
                        }
                    case "INJECTC":
                    {
                        // INJECTC DOWN <intcode>
                        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            client.TrySend("ERR INJECTC syntax"); 
                            Console.WriteLine($"ERR INJECTC syntax");
                            break;
                        }

                        var op = parts[0].ToUpperInvariant();
                        if (!int.TryParse(parts[1], out var codeInt) || codeInt < 0 || codeInt > 4096)
                        { 
                            client.TrySend("ERR INJECTC bad_code"); 
                            Console.WriteLine($"ERR INJECTC bad_code");
                            break; 
                        }

                        bool pressed = op == "DOWN";
                        _uinput.EmitKey((ushort)codeInt, pressed);
                        client.TrySend("OK INJECTC");
                        Console.WriteLine($"OK INJECTC");
                        break;
                    }
                    case "RESET":
                        {
                            // release keys pressed by THIS client (prevents stuck keys)
                            foreach (var k in client.GetPressedSnapshot())
                                _uinput.EmitKey(k, pressed: false);

                            client.ClearPressed();
                            client.TrySend("OK RESET");
                            Console.WriteLine($"OK RESET");
                            break;
                        }

                    default:
                        client.TrySend("ERR UNKNOWN_CMD");
                        Console.WriteLine($"ERR UNKNOWN_CMD");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            // release any keys this client left pressed
            foreach (var k in client.GetPressedSnapshot())
            {
                try { _uinput.EmitKey(k, pressed: false); } catch { /* ignore */ }
            }

            client.Dispose();

            lock (_clientGate) _clients.Remove(client);

            Console.WriteLine("Client disconnected");
        }
    }

    private bool TryHandleInject(string rest, ClientConnection client, out string error)
    {
        // INJECT DOWN <key>
        // INJECT UP <key>
        error = "";

        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            error = "syntax: INJECT DOWN <key> | INJECT UP <key>";
            return false;
        }

        var op = parts[0].ToUpperInvariant();
        var keyTok = parts[1];

        if (!KeyMaps.InjectKeyMap.TryGetValue(keyTok, out var code))
        {
            error = $"unknown_key {keyTok}";
            return false;
        }

        bool pressed;
        if (op == "DOWN") pressed = true;
        else if (op == "UP") pressed = false;
        else
        {
            error = "op must be DOWN or UP";
            return false;
        }

        _uinput.EmitKey(code, pressed);

        if (pressed) client.MarkPressed(code);
        else client.MarkReleased(code);

        return true;
    }

    private static (string cmd, string rest) SplitCmd(string line)
    {
        int sp = line.IndexOf(' ');
        if (sp < 0) return (line.Trim().ToUpperInvariant(), "");
        return (line[..sp].Trim().ToUpperInvariant(), line[(sp + 1)..]);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aa = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);

        int diff = aa.Length ^ bb.Length;
        int n = Math.Min(aa.Length, bb.Length);

        for (int i = 0; i < n; i++)
            diff |= aa[i] ^ bb[i];

        return diff == 0;
    }
}
