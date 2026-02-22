#nullable enable
#if LINUX
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RuneReader.Classes.Platform.Linux;
// This inputd connection is required becuase of wayland.   
// in X11 we can just make calls to X11 and have it handle it.

internal sealed class InputdConnection : IDisposable
{
    private readonly string _socketPath;
    private readonly string _sharedKey;

    private Socket? _sock;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _readTask;

    // Pending request/response completions (FIFO)
    private readonly ConcurrentQueue<TaskCompletionSource<string>> _pending = new();

    public event Action<string>? LineReceived; // for EVT lines (optional)

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
        try
        {
            _sock.Connect(new UnixDomainSocketEndPoint(_socketPath));
        }
        catch (Exception ex)
        {
            Console.WriteLine(String.Concat ("Could not connect to keyboard server\n ", ex.Message));
        }

        _stream = new NetworkStream(_sock, ownsSocket: true);
        _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));

        var authResp = SendAndReadLine($"AUTH {_sharedKey}", expectOkPrefix: "OK AUTH");
    }

    public string SendAndReadLine(string line, string? expectOkPrefix = null)
        => SendAndReadLineAsync(line, expectOkPrefix).GetAwaiter().GetResult();

    public async Task<string> SendAndReadLineAsync(string line, string? expectOkPrefix = null)
    {
        if (_writer == null)
            throw new InvalidOperationException("InputdConnection is not connected.");

        // Create response waiter FIRST (so we can't miss the response)
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(tcs);

        // Serialize writes (only the writer, reads are owned by ReadLoop)
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        
        // Wait for the read loop to deliver the next response line
        var resp = await tcs.Task.ConfigureAwait(false);

        if (expectOkPrefix != null && !resp.StartsWith(expectOkPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Daemon command failed: '{resp}'");

        return resp;
    }

    private static bool IsResponseLine(string line) =>
        line.StartsWith("OK", StringComparison.Ordinal) ||
        line.StartsWith("ERR", StringComparison.Ordinal);
    
    private static bool IsUnsolicited(string line) =>
        line.StartsWith("EVT ", StringComparison.Ordinal) ||
        line.StartsWith("HELLO ", StringComparison.Ordinal) ||
        line.StartsWith("INFO ", StringComparison.Ordinal) ||
        line.StartsWith("WARN ", StringComparison.Ordinal);
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                if (line.Length == 0) continue;

                // 1) Unsolicited lines never satisfy a pending command waiter.
                if (IsUnsolicited(line) || !IsResponseLine(line))
                {
                    try { LineReceived?.Invoke(line); } catch { }
                    continue;
                }

                // 2) Response lines satisfy the oldest waiter (if any)
                if (_pending.TryDequeue(out var tcs))
                {
                    tcs.TrySetResult(line);
                }
                else
                {
                    // Response with no waiter — log it as unsolicited
                    try { LineReceived?.Invoke(line); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            // fail all waiters on read loop death
            while (_pending.TryDequeue(out var tcs))
                tcs.TrySetException(ex);
        }
        finally
        {
            // connection closed: fail all waiters
            while (_pending.TryDequeue(out var tcs))
                tcs.TrySetException(new IOException("Daemon connection closed."));
        }
    }
   
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _readTask?.Wait(200); } catch { }

        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _sock?.Dispose(); } catch { }

        _cts = null;
        _readTask = null;
        _writer = null;
        _reader = null;
        _stream = null;
        _sock = null;
    }
}
#endif

