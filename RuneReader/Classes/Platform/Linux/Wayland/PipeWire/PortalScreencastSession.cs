using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;

namespace RuneReader.Classes.Platform.Linux.Wayland.PipeWire;

public sealed class PortalScreencastSession : IAsyncDisposable
{
    private readonly Connection _bus;
    private readonly IPortal _portal;
    private readonly IProperties _portalProps;

    private ObjectPath? _sessionPath;
    private ObjectPath? _requestPath;

    public sealed record Result(SafeHandle PipeWireFd, uint NodeId, int Width, int Height);

    public PortalScreencastSession()
    {
        _bus = new Connection(Address.Session);
        _portal = _bus.CreateProxy<IPortal>("org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop");
        _portalProps =
            _bus.CreateProxy<IProperties>("org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop");
    }

    public async Task<Result> StartAsync(CancellationToken ct)
    {
        await _bus.ConnectAsync();

        // Portal version checks (optional)
        // var version = await _portalProps.GetAsync("org.freedesktop.portal.ScreenCast", "version");

        var token = Guid.NewGuid().ToString("N");
        var handleToken = token;

        var options = new Dictionary<string, object>
        {
            ["handle_token"] = handleToken,
            // For many portals, "types" is required: 1=monitor, 2=window, 4=virtual
            ["types"] = (uint)1, // monitor
            ["multiple"] = false,
            ["cursor_mode"] = (uint)2, // 0=hidden,1=embedded,2=metadata (metadata tends to be easiest)
        };

        // CreateSession
        _requestPath = await _portal.CreateSessionAsync(options);
        var createRes = await WaitRequestAsync(_requestPath.Value, ct);

        _sessionPath = (ObjectPath)createRes["session_handle"];

        // SelectSources
        var selOptions = new Dictionary<string, object>
        {
            ["handle_token"] = Guid.NewGuid().ToString("N"),
            ["types"] = (uint)1,
            ["multiple"] = false,
        };
        var selectReq = await _portal.SelectSourcesAsync(_sessionPath.Value, selOptions);
        await WaitRequestAsync(selectReq, ct);

        // Start
        var startOptions = new Dictionary<string, object>
        {
            ["handle_token"] = Guid.NewGuid().ToString("N"),
        };
        var startReq = await _portal.StartAsync(_sessionPath.Value, "", startOptions);
        var startRes = await WaitRequestAsync(startReq, ct);

        // "streams" is array of (node_id, dict props)
        // We only asked for one monitor, so take first.
        var streams = (ValueTuple<uint, IDictionary<string, object>>[])startRes["streams"];
        if (streams.Length == 0)
            throw new InvalidOperationException("Portal returned no streams.");

        uint nodeId = streams[0].Item1;
        var props = streams[0].Item2;

        int width = props.TryGetValue("size", out var sizeObj) && sizeObj is ValueTuple<int, int> size
            ? size.Item1
            : 0;
        int height = props.TryGetValue("size", out var sizeObj2) && sizeObj2 is ValueTuple<int, int> size2
            ? size2.Item2
            : 0;

        // OpenPipeWireRemote (returns a unix fd handle)
        var fdOptions = new Dictionary<string, object>();
        var pwFd = await _portal.OpenPipeWireRemoteAsync(_sessionPath.Value, fdOptions);

        return new Result(pwFd, nodeId, width, height);
    }

    private async Task<IDictionary<string, object>> WaitRequestAsync(ObjectPath requestPath, CancellationToken ct)
    {
        // Requests are signaled on /org/freedesktop/portal/desktop/request/<sender>/<token>
        // We subscribe to org.freedesktop.portal.Request::Response
        var req = _bus.CreateProxy<IRequest>("org.freedesktop.portal.Desktop", requestPath);

        var tcs = new TaskCompletionSource<(uint response, IDictionary<string, object> results)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = await req.WatchResponseAsync((response, results) => { tcs.TrySetResult((response, results)); });

        using (ct.Register(() => tcs.TrySetCanceled(ct)))
        {
            var (response, results) = await tcs.Task.ConfigureAwait(false);
            if (response != 0)
                throw new InvalidOperationException($"Portal request failed. Response={response}");
            return results;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_sessionPath is not null)
            {
                await _portal.CloseSessionAsync(_sessionPath.Value);
                _sessionPath = null;
            }
        }
        catch
        {
            /* best effort */
        }

        _bus.Dispose();
    }

    // DBus interfaces
    [DBusInterface("org.freedesktop.portal.Desktop")]
    private interface IPortal : IDBusObject
    {
        Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
        Task<ObjectPath> SelectSourcesAsync(ObjectPath session, IDictionary<string, object> options);
        Task<ObjectPath> StartAsync(ObjectPath session, string parentWindow, IDictionary<string, object> options);
        Task<SafeHandle> OpenPipeWireRemoteAsync(ObjectPath session, IDictionary<string, object> options);
        Task CloseSessionAsync(ObjectPath session);
    }

    [DBusInterface("org.freedesktop.portal.Request")]
    private interface IRequest : IDBusObject
    {
        Task<IDisposable> WatchResponseAsync(Action<uint, IDictionary<string, object>> handler);
    }

    [DBusInterface("org.freedesktop.DBus.Properties")]
    private interface IProperties : IDBusObject
    {
        Task<object> GetAsync(string iface, string prop);
    }
}