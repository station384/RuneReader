#nullable enable
using System.Diagnostics;
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

    
    public async Task RunAsync(CancellationToken ct)
    {
        // Remove stale socket file before bind (common after crashes)
        try { Sys.unlink(_socketPath); } catch { /* ignore */ }
        
        var ep = new UnixDomainSocketEndPoint(_socketPath);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            listener.Bind(ep);

            // Permissions: 0660 on the socket file so your app group can connect.
            // For now I am using 0666 so any app can use it.   Less secure but that is what I want for now.
            // I'm breaking so many "secure" rules just by doing this wayland workaround anyway.

            try
            {
                Sys.chmod(_socketPath, Convert.ToUInt32("666", 8));
            }
            catch
            {
                /* ignore */
            }

            listener.Listen(backlog: 50);

            Console.WriteLine("Listening...");

            while (!ct.IsCancellationRequested)
            {
                Socket socket;
                try
                {
                     socket = await listener.AcceptAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(socket));
            }
        }
        finally
        {
            // Best effort: release any possibly held keys on daemon exit
            try { _uinput.ReleaseAllEnabled(); }
            catch { }

            // Remove socket file on exit
            try { Sys.unlink(_socketPath); } 
            catch { }
        }
    }

    private void OnMonitorKeyEvent(object? sender, EvdevKeyboardMonitor.KeyEventArgs e)
    {
        // Activation key only
        if (e.Code == _activationKeyCode)
        {
            Broadcast($"ACT {(e.Pressed ? "DOWN" : "UP")}");
            Debug.WriteLine($" ACT: {(e.Pressed ? "ON" : "OFF")}");
        }

        // Modifiers (optional but useful for parity with your IGlobalHotkeys)
        if (KeyMaps.ModifierCodeToName.TryGetValue(e.Code, out var mod))
        {
            Broadcast($"MOD {mod} {(e.Pressed ? "DOWN" : "UP")}");
            Debug.WriteLine($" MOD: {mod} {(e.Pressed ? "DOWN" : "UP")}");

        }
    }

    private void Broadcast(string line)
    {
        List<ClientConnection> snapshot;
        lock (_clientGate) 
            snapshot = _clients.ToList();

        foreach (var c in snapshot)
            c.TrySend(line);
    }

    

static bool TryParse2(string s, out string a, out string b)
    {
        s = s.Trim();
        int i = s.IndexOf(' ');
        if (i < 0) { a = ""; b = ""; return false; }
        a = s.Substring(0, i);
        b = s.Substring(i + 1).Trim();
        return true;
    }
// This is best but only works for c# 13+
    // static bool TryParse2(string s, out ReadOnlySpan<char> a, out ReadOnlySpan<char> b)
    // {
    //     var span = s.AsSpan().Trim();
    //     int i = span.IndexOf(' ');
    //     if (i < 0) { a = default; b = default; return false; }
    //     a = span[..i];
    //     b = span[(i + 1)..].Trim();
    //     return true;
    // }
    
    private async Task HandleClientAsync(Socket socket)
    {
        var client = new ClientConnection(socket);

        lock (_clientGate) 
            _clients.Add(client);

        Console.WriteLine("Client connected");

        try
        {
            client.TrySend("HELLO runereader-inputd 1");
            client.TrySend("AUTH_REQUIRED 1");

            bool authed = false;
            int cycleCount = 0;
            while (true)
            {
                cycleCount++;
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
                            Debug.WriteLine($"AUTH: OK");
                        }
                        else
                        {
                            client.TrySend("ERR AUTH");
                            Debug.WriteLine($"AUTH: ERR");
                            break;
                        }
                    }
                    else
                    {
                        client.TrySend("ERR NOT_AUTHED");
                        Debug.WriteLine($"AUTH: ERR NOT_AUTHED");

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
                            Debug.WriteLine($"OK SET_ACTKEY {tok.ToUpperInvariant()}");
                            break;
                        }

                    case "INJECT":
                        {
                            if (!TryHandleInject(rest, client, out var err))
                            {
                                client.TrySend($"ERR INJECT {err}");
                                Debug.WriteLine($"ERR INJECT {err}");

                            }
                            else
                            {
                                client.TrySend("OK INJECT");
                                Debug.WriteLine($"OK INJECT");

                            }
                            break;
                        }
                    case "INJECTC":
                    {
                        // INJECTC DOWN <intcode>
                        //var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (TryParse2(rest, out  var _part1, out  var _part2))
                        {
                            // if (parts.Length != 2)
                            // {
                            //     client.TrySend("ERR INJECTC syntax");
                            //     Debug.WriteLine($"ERR INJECTC syntax");
                            //     break;
                            // }

                            var op = _part1.ToUpperInvariant();
                            if (!int.TryParse(_part2, out var codeInt) || codeInt < 0 || codeInt > 4096)
                            {
                                client.TrySend("ERR INJECTC bad_code");
                                Debug.WriteLine($"ERR INJECTC bad_code");
                                break;
                            }


                            bool pressed = op == "DOWN";
                            _uinput.EmitKey((ushort)codeInt, pressed);

                            // Track for stuck-key safety.
                            if (pressed) client.MarkPressed((ushort)codeInt);
                            else client.MarkReleased((ushort)codeInt);

                            client.TrySend("OK INJECTC");
                            Debug.WriteLine($"OK INJECTC {codeInt} : {op}");
                        }
                        else
                        {
                            client.TrySend("ERR INJECTC syntax");
                            Debug.WriteLine($"ERR INJECTC syntax");
                            break;
                        }

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

                if (cycleCount >= 50)
                {
                    cycleCount = 0;
                    GC.Collect();
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
            try
            {
                // 1) release keys we believe this client is holding
                _uinput.ReleaseKeys(client.GetPressedSnapshot());

                // 2) belt-and-suspenders: release all enabled keys (guards against missed tracking)
                _uinput.ReleaseAllEnabled();
            }
            catch { /* ignore */ }

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
