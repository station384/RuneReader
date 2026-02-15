#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tmds.DBus;
using Tmds.DBus.Protocol;
using Connection = Tmds.DBus.Connection;
using ObjectPath = Tmds.DBus.ObjectPath;
using Microsoft.Win32.SafeHandles;


namespace RuneReader.Classes.Platform.Linux.Wayland;

/// <summary>
/// Manages xdg-desktop-portal ScreenCast session and exposes PipeWire fd + node id.
/// </summary>
internal sealed class PortalScreenCastSession : IDisposable
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalPath = new ObjectPath("/org/freedesktop/portal/desktop");

    private readonly Connection _bus;
    //private readonly IPortalScreenCast _screenCast;

    public string SessionHandle { get; }
    // public uint PipeWireFd { get; }
    public uint NodeId { get; }

    public SafeFileHandle PipeWireHandle { get; }
    public int PipeWireFd { get; }
        
    private static string GetRealSessionBusAddress()
    {
        var addr = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS") ?? "";
        var xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "";

        // If we’re not in Flatpak but DBUS points at /run/flatpak/bus, it’s almost certainly wrong.
        var inFlatpak = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATPAK_ID")) ||
                        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("container"));

        if (!inFlatpak && addr.Contains("/run/flatpak/bus", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(xdg))
                return $"unix:path={xdg}/bus";

            // last resort fallback (uid 1000 here is example; use getuid if you want)
            return "unix:path=/run/user/1000/bus";
        }

        // If DBUS address missing but XDG exists, synthesize it
        if (string.IsNullOrWhiteSpace(addr) && !string.IsNullOrWhiteSpace(xdg))
            return $"unix:path={xdg}/bus";

        return addr;
    }
    private static async Task ConnectWithTimeout(Connection bus, int timeoutMs)
    {
        var connectTask = bus.ConnectAsync();
        var done = await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
        if (done != connectTask)
            throw new TimeoutException($"D-Bus ConnectAsync timed out after {timeoutMs}ms.");
        await connectTask.ConfigureAwait(false);
    }
    private readonly ObjectPath _sessionHandlePath;
    private PortalScreenCastSession(Connection bus, ObjectPath sessionHandlePath, string sessionHandle, SafeFileHandle pipeWireHandle, uint nodeId)
    {
        _bus = bus;
        //_screenCast = screenCast;
        SessionHandle = sessionHandle;
        //PipeWireFd = pipeWireFd;
        PipeWireHandle = pipeWireHandle;
        PipeWireFd = pipeWireHandle.DangerousGetHandle().ToInt32();
        NodeId = nodeId;
        _sessionHandlePath = sessionHandlePath;

    }

    public static async Task<PortalScreenCastSession> CreateAndStartAsync(int screenNumber)
    {
        // Note: "screenNumber" is not always honored by portals the same way across compositors.
        // For MONITOR source selection, the portal picker is the authority.
        //var bus = new Connection(Address.Session);
        var addr = GetRealSessionBusAddress();
        Debug.WriteLine($"DBUS resolved address={addr}");
        var bus = new Connection(addr);
            
        Debug.WriteLine($"DBUS_SESSION_BUS_ADDRESS={Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")}");
        Debug.WriteLine($"XDG_RUNTIME_DIR={Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")}");
        Debug.WriteLine($"WAYLAND_DISPLAY={Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")}");
        Debug.WriteLine($"DBUS resolved address={addr}");
            
        await ConnectWithTimeout(bus, 5000).ConfigureAwait(false);
          

        var screenCast = bus.CreateProxy<IPortalScreenCast>(PortalBusName, PortalPath);
    

        // CreateSession
        string tokenBase = "runereader_" + Guid.NewGuid().ToString("N");
        var createOptions = new Dictionary<string, object>
        {
            ["handle_token"] = tokenBase + "_create",
            ["session_handle_token"] = tokenBase + "_session"
        };

        var createRequestPath = await screenCast.CreateSessionAsync(createOptions);
        var createResults = await WaitRequestAsync(bus, createRequestPath).ConfigureAwait(false);


        if (!createResults.TryGetValue("session_handle", out var sessionHandleObj))
            throw new InvalidOperationException("Portal CreateSession did not return session_handle.");
      
        Debug.WriteLine("CreateSession results:");
        foreach (var kv in createResults)
            Debug.WriteLine($"  {kv.Key} = {kv.Value} ({kv.Value?.GetType().FullName ?? "null"})");
            
        ObjectPath sessionHandlePath =
            sessionHandleObj switch
            {
                ObjectPath op => op, // some backends might still send an object path
                string s when !string.IsNullOrWhiteSpace(s) => new ObjectPath(s),
                _ => throw new InvalidOperationException(
                    $"Portal CreateSession returned session_handle of unexpected type: {sessionHandleObj?.GetType().FullName ?? "null"}")
            };

        string sessionHandle = sessionHandlePath.ToString();

            
        // SelectSources (MONITOR)
        // types: 1=MONITOR
        // multiple: false (single stream)
        // cursor_mode: 2 (embedded) or 1 (metadata). We'll use embedded for simplicity.
        var selectOptions = new Dictionary<string, object>
        {
            ["handle_token"] = tokenBase + "_select",
            ["types"] = (uint)1,
            ["multiple"] = false,
            ["cursor_mode"] = (uint)2
        };

        var selectRequestPath = await screenCast.SelectSourcesAsync(sessionHandlePath, selectOptions);
        _ = await WaitRequestAsync(bus, selectRequestPath).ConfigureAwait(false);

        // Start
        var startOptions = new Dictionary<string, object>
        {
            ["handle_token"] = tokenBase + "_start"
        };

        // parent_window: empty string is allowed; some portals use it for focus/stacking. If you can provide X11/Wayland handle later, great.
        var startRequestPath = await screenCast.StartAsync(sessionHandlePath, "", startOptions);
        var startResults = await WaitRequestAsync(bus, startRequestPath).ConfigureAwait(false);

        // Extract streams: a(ua{sv})
        if (!startResults.TryGetValue("streams", out var streamsObj))
            throw new InvalidOperationException("Portal Start did not return streams.");

        var (nodeId, width, height) = ParseFirstStream(streamsObj);


        var pwHandle = await screenCast.OpenPipeWireRemoteAsync(sessionHandlePath, new Dictionary<string, object>());

        var fdInt = pwHandle.DangerousGetHandle().ToInt32();
        Debug.WriteLine($"OpenPipeWireRemote fd={fdInt} invalid={pwHandle.IsInvalid}");

        if (fdInt <= 2)
            throw new InvalidOperationException($"Portal returned suspicious PipeWire fd={fdInt}");

        return new PortalScreenCastSession(bus, sessionHandlePath, sessionHandle, pwHandle, nodeId);
    }

        
    private static object UnwrapVariant(object v)
    {
        // In results dictionaries coming from portals, values are commonly VariantValue.
        if (v is VariantValue vv)
        {
            // Only unwrap if it's a "variant-of-variant".
            // Otherwise vv itself is the payload container.
            if (vv.Type == VariantValueType.Variant)
                return vv.GetVariantValue();

            return vv; // keep it as VariantValue
        }

        return v;
    }


    private static (uint nodeId, int width, int height) ParseFirstStream(object streamsObj)
    {
        streamsObj = UnwrapVariant(streamsObj);

        // Case 1: already strongly typed
        if (streamsObj is (uint NodeId, IDictionary<string, object> Props)[] vtArr && vtArr.Length > 0)
            return ParseFromProps(vtArr[0].NodeId, vtArr[0].Props);

        // Case 2: IEnumerable of strongly typed tuples
        if (streamsObj is IEnumerable<(uint NodeId, IDictionary<string, object> Props)> vtEnum)
        {
            foreach (var item in vtEnum)
                return ParseFromProps(item.NodeId, item.Props);
        }

        // Case 3: object[] where each element is a 2-item tuple/object[]
        if (streamsObj is object[] objArr && objArr.Length > 0)
            return ParseStreamElement(objArr[0]);

        // Case 4: IEnumerable<object>
        if (streamsObj is System.Collections.IEnumerable enumObj)
        {
            foreach (var item in enumObj)
                return ParseStreamElement(item);
        }

        throw new InvalidOperationException(
            $"Portal streams has unexpected type: {streamsObj?.GetType().FullName ?? "null"}");
    }

    private static (uint nodeId, int width, int height) ParseStreamElement(object? streamElement)
    {
        if (streamElement is null)
            throw new InvalidOperationException("Portal stream element is null.");

        streamElement = UnwrapVariant(streamElement);

        // (u, a{sv}) can come as object[] { nodeId, props }
        if (streamElement is object[] tuple && tuple.Length == 2)
        {
            uint nodeId = Convert.ToUInt32(UnwrapVariant(tuple[0]));
            var props = UnwrapVariant(tuple[1]) as IDictionary<string, object>;
            if (props == null)
                throw new InvalidOperationException($"Portal stream props not a dictionary: {tuple[1]?.GetType().FullName ?? "null"}");

            return ParseFromProps(nodeId, props);
        }

        // Or as ValueTuple<uint, IDictionary<string, object>>
        if (streamElement is ValueTuple<uint, IDictionary<string, object>> vt)
            return ParseFromProps(vt.Item1, vt.Item2);

        // Or as Tuple<uint, IDictionary<string, object>>
        if (streamElement is Tuple<uint, IDictionary<string, object>> tup)
            return ParseFromProps(tup.Item1, tup.Item2);

        throw new InvalidOperationException(
            $"Unrecognized stream element shape: {streamElement.GetType().FullName}");
    }

    private static (uint nodeId, int width, int height) ParseFromProps(uint nodeId, IDictionary<string, object> props)
    {
        int w = 0, h = 0;

        object? sizeObj = null;
        if (props.TryGetValue("size", out sizeObj) && sizeObj != null)
        {
            sizeObj = UnwrapVariant(sizeObj);

            // size might be (ii) as object[] or ValueTuple<int,int> or int[]
            if (sizeObj is object[] sz && sz.Length == 2)
            {
                w = Convert.ToInt32(UnwrapVariant(sz[0]));
                h = Convert.ToInt32(UnwrapVariant(sz[1]));
            }
            else if (sizeObj is int[] ia && ia.Length == 2)
            {
                w = ia[0]; h = ia[1];
            }
            else if (sizeObj is ValueTuple<int, int> vt)
            {
                w = vt.Item1; h = vt.Item2;
            }
            else if (sizeObj is Tuple<int, int> tup)
            {
                w = tup.Item1; h = tup.Item2;
            }
            // otherwise ignore; we’ll try width/height keys below
        }

        if (w == 0 && props.TryGetValue("width", out var wObj))
            w = Convert.ToInt32(UnwrapVariant(wObj));
        if (h == 0 && props.TryGetValue("height", out var hObj))
            h = Convert.ToInt32(UnwrapVariant(hObj));

        return (nodeId, w, h);
    }        
        
        
    private static async Task<IDictionary<string, object>> WaitRequestAsync(Connection bus, ObjectPath requestPath)
    {
        var request = bus.CreateProxy<IPortalRequest>(PortalBusName, requestPath);

        var tcs = new TaskCompletionSource<(uint response, IDictionary<string, object> results)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var sub = await request.WatchResponseAsync(
            payload => tcs.TrySetResult((payload.Response, payload.Results)),
            ex => tcs.TrySetException(ex)
        );

        var (response, dict) = await tcs.Task.ConfigureAwait(false);

        if (response != 0)
            throw new InvalidOperationException($"Portal request failed. response={response}");

        return dict;
    }

    public void Dispose()
    {
            
        try
        {
            var session = _bus.CreateProxy<IPortalSession>(PortalBusName, _sessionHandlePath);
            session.CloseAsync().GetAwaiter().GetResult();
        }
        catch { /* ignore */ }

        try { PipeWireHandle?.Dispose(); } catch { /* ignore */ }
        // Connection disposal is optional; we keep it simple and close it.
        try { _bus.Dispose(); } catch { /* ignore */ }
    }
}

[DBusInterface("org.freedesktop.portal.ScreenCast")]
public interface IPortalScreenCast : IDBusObject
{
    // CreateSession(a{sv}) -> o (request object path)
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);

    // SelectSources(o session_handle, a{sv}) -> o (request)
    Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

    // Start(o session_handle, s parent_window, a{sv}) -> o (request)
    Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);

    // OpenPipeWireRemote(o session_handle, a{sv}) -> h (Unix fd)
    Task<SafeFileHandle> OpenPipeWireRemoteAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
}

// [DBusInterface("org.freedesktop.portal.Request")]
// public interface IPortalRequest : IDBusObject
// {
//     // signal Response(u, a{sv})
//     Task<IDisposable> WatchResponseAsync(Action<uint, IDictionary<string, object>> handler);
// }
    
[DBusInterface("org.freedesktop.portal.Request")]
public interface IPortalRequest : IDBusObject
{
    // signal Response(u, a{sv})
    Task<IDisposable> WatchResponseAsync(
        Action<(uint Response, IDictionary<string, object> Results)> handler,
        Action<Exception>? onError = null);
}
    
[DBusInterface("org.freedesktop.portal.Session")]
public interface IPortalSession : IDBusObject
{
    Task CloseAsync();
}