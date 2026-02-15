#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Tmds.DBus;

namespace RuneReader.Classes.Platform.Linux.Wayland
{
    /// <summary>
    /// Manages xdg-desktop-portal ScreenCast session and exposes PipeWire fd + node id.
    /// </summary>
    internal sealed class PortalScreenCastSession : IDisposable
    {
        private const string PortalBusName = "org.freedesktop.portal.Desktop";
        private static readonly ObjectPath PortalPath = new ObjectPath("/org/freedesktop/portal/desktop");

        private readonly Connection _bus;
        private readonly IPortalScreenCast _screenCast;

        public string SessionHandle { get; }
        public uint PipeWireFd { get; }
        public uint NodeId { get; }

        private PortalScreenCastSession(Connection bus, IPortalScreenCast screenCast, string sessionHandle, uint pipeWireFd, uint nodeId)
        {
            _bus = bus;
            _screenCast = screenCast;
            SessionHandle = sessionHandle;
            PipeWireFd = pipeWireFd;
            NodeId = nodeId;
        }

        public static async Task<PortalScreenCastSession> CreateAndStartAsync(int screenNumber)
        {
            // Note: "screenNumber" is not always honored by portals the same way across compositors.
            // For MONITOR source selection, the portal picker is the authority.
            var bus = new Connection(Address.Session);
            await bus.ConnectAsync();

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

            if (!createResults.TryGetValue("session_handle", out var sessionHandleObj) || sessionHandleObj is not ObjectPath sessionHandlePath)
                throw new InvalidOperationException("Portal CreateSession did not return session_handle.");

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

            // OpenPipeWireRemote returns a Unix FD (type 'h')
            var fd = await screenCast.OpenPipeWireRemoteAsync(sessionHandlePath, new Dictionary<string, object>());

            uint pipewireFd = fd; // Tmds.DBus UnixFd wrapper

            Debug.WriteLine($"[PortalScreenCastSession] nodeId={nodeId}, w={width}, h={height}, fd={pipewireFd}");

            return new PortalScreenCastSession(bus, screenCast, sessionHandle, pipewireFd, nodeId);
        }

        private static (uint nodeId, int width, int height) ParseFirstStream(object streamsObj)
        {
            // Tmds.DBus will typically materialize complex variants as:
            // object[] where each element is object[] representing (u, IDictionary<string, object>)
            // We'll handle a few common shapes defensively.

            // Expected: object[] streams
            if (streamsObj is not object[] streams || streams.Length == 0)
                throw new InvalidOperationException("Portal streams is empty or not an array.");

            var stream0 = streams[0];

            // Each stream: (u node_id, a{sv} props)
            if (stream0 is object[] tuple && tuple.Length == 2)
            {
                uint nodeId = Convert.ToUInt32(tuple[0]);
                var props = tuple[1] as IDictionary<string, object>;

                int w = 0, h = 0;

                // Known keys (depending on portal/compositor):
                // "size" might be (ii) or int[] {w,h}
                // Sometimes "width"/"height" exist.
                if (props != null)
                {
                    if (props.TryGetValue("size", out var sizeObj))
                    {
                        if (sizeObj is object[] sz && sz.Length == 2)
                        {
                            w = Convert.ToInt32(sz[0]);
                            h = Convert.ToInt32(sz[1]);
                        }
                        else if (sizeObj is int[] ia && ia.Length == 2)
                        {
                            w = ia[0]; h = ia[1];
                        }
                    }

                    if (w == 0 && props.TryGetValue("width", out var wObj)) w = Convert.ToInt32(wObj);
                    if (h == 0 && props.TryGetValue("height", out var hObj)) h = Convert.ToInt32(hObj);
                }

                return (nodeId, w, h);
            }

            throw new InvalidOperationException("Unrecognized streams element shape.");
        }

        private static async Task<IDictionary<string, object>> WaitRequestAsync(Connection bus, ObjectPath requestPath)
        {
            // org.freedesktop.portal.Request.Response: (u response, a{sv} results)
            var request = bus.CreateProxy<IPortalRequest>(PortalBusName, requestPath);

            var tcs = new TaskCompletionSource<(uint response, IDictionary<string, object> results)>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var sub = await request.WatchResponseAsync((resp, results) =>
            {
                tcs.TrySetResult((resp, results));
            });

            var (response, dict) = await tcs.Task.ConfigureAwait(false);

            // 0 = success
            if (response != 0)
                throw new InvalidOperationException($"Portal request failed. response={response}");

            return dict;
        }

        public void Dispose()
        {
            // Connection disposal is optional; we keep it simple and close it.
            try { _bus.Dispose(); } catch { /* ignore */ }
        }
    }

    [DBusInterface("org.freedesktop.portal.ScreenCast")]
    internal interface IPortalScreenCast : IDBusObject
    {
        // CreateSession(a{sv}) -> o (request object path)
        Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);

        // SelectSources(o session_handle, a{sv}) -> o (request)
        Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

        // Start(o session_handle, s parent_window, a{sv}) -> o (request)
        Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);

        // OpenPipeWireRemote(o session_handle, a{sv}) -> h (Unix fd)
        Task<uint> OpenPipeWireRemoteAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
    }

    [DBusInterface("org.freedesktop.portal.Request")]
    internal interface IPortalRequest : IDBusObject
    {
        // signal Response(u, a{sv})
        Task<IDisposable> WatchResponseAsync(Action<uint, IDictionary<string, object>> handler);
    }
}
