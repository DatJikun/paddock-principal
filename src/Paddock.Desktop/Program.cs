using Paddock.Desktop.Bridge;

namespace Paddock.Desktop;

internal static class Program
{
    private const int DefaultPort = 4731;

    [STAThread]
    public static int Main(string[] args)
    {
        var dev = false;
        var port = DefaultPort;
        var year = CareerBridge.DefaultYear;
        var seed = CareerBridge.DefaultSeed;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dev":
                    dev = true;
                    break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[++i], out var parsed) && parsed > 0:
                    port = parsed;
                    break;
                case "--year" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedYear):
                    year = parsedYear;
                    break;
                case "--seed" when i + 1 < args.Length && ulong.TryParse(args[++i], out var parsedSeed):
                    seed = parsedSeed;
                    break;
                default:
                    Console.Error.WriteLine("Unknown argument: " + args[i]);
                    return 2;
            }
        }

        var root = BridgeHost.RepositoryRoot();
        var host = BridgeHost.Open(Path.Combine(root, "data"), year, seed);
        var ui = Path.Combine(root, "ui", "app", "dist");
        using var server = new DevServer(host, ui, DevServer.FindPort(port));
        server.Start();
        Console.WriteLine("bridge " + server.Url);
        Console.WriteLine("websocket " + server.WebSocketUrl);
        if (dev)
        {
            Console.WriteLine("dev mode: open the UI in a browser. Ctrl+C stops the bridge.");
            var done = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                done.Set();
            };
            done.Wait();
            return 0;
        }

        return WindowHost.Run(server.Url, host);
    }
}
