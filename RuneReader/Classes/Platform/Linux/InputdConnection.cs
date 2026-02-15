#nullable enable
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RuneReader.InputD.Client;

internal sealed class InputdConnection : IDisposable
{
    private readonly string _socketPath;
    private readonly string _sharedKey;

    private Socket? _sock;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public event Action<string>? LineReceived;

    public bool IsConnected => _sock is { Connected: true } && _reader != null && _writer != null;

    public InputdConnection(string socketPath, string sharedKey)
    {
        _socketPath = socketPath;
        _sharedKey = sharedKey;
    }

    public void Connect()
    {
        if (IsConnected) return;

        _sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _sock.Connect(new UnixDomainSocketEndPoint(_socketPath));

        _stream = new NetworkStream(_sock, ownsSocket: true);
        _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        // non-strict: consume banner lines if present
        _ = _reader.ReadLine();
        _ = _reader.ReadLine();

        // auth
        SendAndReadLine($"AUTH {_sharedKey}", expectOkPrefix: "OK AUTH");

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public string SendAndReadLine(string line, string? expectOkPrefix = null)
    {
        _sendLock.Wait();
        try
        {
            if (_writer == null || _reader == null)
                throw new InvalidOperationException("InputdConnection is not connected.");

            _writer.WriteLine(line);

            var resp = _reader.ReadLine();
            if (resp == null)
                throw new IOException("Daemon connection closed.");

            if (expectOkPrefix != null && !resp.StartsWith(expectOkPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Daemon command failed: '{resp}'");

            return resp;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<string> SendAndReadLineAsync(string line, string? expectOkPrefix = null)
    {
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer == null || _reader == null)
                throw new InvalidOperationException("InputdConnection is not connected.");

            await _writer.WriteLineAsync(line).ConfigureAwait(false);

            var resp = await _reader.ReadLineAsync().ConfigureAwait(false);
            if (resp == null)
                throw new IOException("Daemon connection closed.");

            if (expectOkPrefix != null && !resp.StartsWith(expectOkPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Daemon command failed: '{resp}'");

            return resp;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _reader!.ReadLineAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            if (line == null) break;
            if (line.Length == 0) continue;

            try { LineReceived?.Invoke(line); } catch { /* never let listeners kill loop */ }
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listenTask?.Wait(200); } catch { }

        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _sock?.Dispose(); } catch { }

        _cts = null;
        _listenTask = null;
        _writer = null;
        _reader = null;
        _stream = null;
        _sock = null;
    }
}
