#nullable enable
using System.Net.Sockets;
using System.Text;

namespace runereader_inputd;

internal sealed class ClientConnection : IDisposable
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private readonly object _sendGate = new();

    private readonly object _pressedGate = new();
    private readonly HashSet<ushort> _pressed = new();

    public ClientConnection(Socket socket)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: true);
        _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
    }

    public void TrySend(string line)
    {
        lock (_sendGate)
        {
            try { _writer.WriteLine(line); }
            catch { /* ignore */ }
        }
    }

    public async Task<string?> ReadLineAsync()
    {
        try { return await _reader.ReadLineAsync(); }
        catch { return null; }
    }

    public void MarkPressed(ushort code)
    {
        lock (_pressedGate) _pressed.Add(code);
    }

    public void MarkReleased(ushort code)
    {
        lock (_pressedGate) _pressed.Remove(code);
    }

    public ushort[] GetPressedSnapshot()
    {
        lock (_pressedGate) return _pressed.ToArray();
    }

    public void ClearPressed()
    {
        lock (_pressedGate) _pressed.Clear();
    }

    public void Dispose()
    {
        try { _writer.Dispose(); } catch { }
        try { _reader.Dispose(); } catch { }
        try { _stream.Dispose(); } catch { }
        try { _socket.Dispose(); } catch { }
    }
}