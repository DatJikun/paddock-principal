using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// Localhost HTTP for the built UI and a WebSocket for the same bridge the window uses (TECH §1.2).
/// Bound to 127.0.0.1 only.
/// </summary>
public sealed class DevServer : IDisposable
{
    private readonly BridgeHost _host;
    private readonly string _uiRoot;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    public DevServer(BridgeHost host, string uiRoot, int port)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(uiRoot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        _host = host;
        _uiRoot = uiRoot;
        Port = port;
        Url = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
        WebSocketUrl = "ws://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/bridge";
        _listener.Prefixes.Add(Url);
    }

    public int Port { get; }

    public string Url { get; }

    public string WebSocketUrl { get; }

    public static int FindPort(int first)
    {
        for (var port = first; port < first + 30; port++)
        {
            try
            {
                var probe = new HttpListener();
                probe.Prefixes.Add("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/");
                probe.Start();
                probe.Stop();
                probe.Close();
                return port;
            }
            catch (HttpListenerException)
            {
            }
        }

        throw new InvalidOperationException("No free localhost port for the bridge.");
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(Listen);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
    }

    private async Task Listen()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException or HttpListenerException)
            {
                return;
            }

            _ = Task.Run(() => Serve(context));
        }
    }

    private async Task Serve(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (string.Equals(path, "/bridge", StringComparison.Ordinal))
            {
                if (context.Request.IsWebSocketRequest)
                {
                    var socket = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                    await SocketLoop(socket.WebSocket).ConfigureAwait(false);
                    return;
                }

                var body = Encoding.UTF8.GetBytes("{\"ws\":\"" + WebSocketUrl + "\"}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                context.Response.Close();
                return;
            }

            await File(context, path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("bridge http: " + ex.Message);
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch (HttpListenerException)
            {
            }
        }
    }

    private async Task SocketLoop(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open && !_stop.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, _stop.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                BridgeExchange exchange;
                try
                {
                    exchange = _host.Handle(Encoding.UTF8.GetString(message.ToArray()));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("bridge: " + ex.GetType().Name);
                    exchange = new BridgeExchange(BridgeValues.Failure("", BridgeKeys.Internal, null), []);
                }

                await Send(socket, exchange.Response).ConfigureAwait(false);
                foreach (var pushed in exchange.Events)
                {
                    await Send(socket, pushed).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static async Task Send(WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task File(HttpListenerContext context, string path)
    {
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(relative))
        {
            relative = "index.html";
        }

        var full = Path.GetFullPath(Path.Combine(_uiRoot, relative));
        var root = Path.GetFullPath(_uiRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        if (!System.IO.File.Exists(full))
        {
            if (string.Equals(relative, "index.html", StringComparison.Ordinal))
            {
                var html = Encoding.UTF8.GetBytes(
                    "<!DOCTYPE html><html><body><p>UI build is not in ui/app/dist yet.</p></body></html>");
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = html.Length;
                await context.Response.OutputStream.WriteAsync(html).ConfigureAwait(false);
                context.Response.Close();
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(full).ConfigureAwait(false);
        context.Response.ContentType = ContentType(full);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".json" => "application/json",
        ".woff2" => "font/woff2",
        ".png" => "image/png",
        _ => "application/octet-stream",
    };
}
