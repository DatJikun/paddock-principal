using Photino.NET;
using Paddock.Desktop.Bridge;

namespace Paddock.Desktop;

/// <summary>Photino window. The page talks to the bridge through the web message channel (TECH §1.2).</summary>
internal static class WindowHost
{
    /// <summary>
    /// The page's "Wyjdź": a window message of its own, not a bridge name, because closing the window is not a game command.
    /// The bridge host never sees it.
    /// </summary>
    internal static bool IsExit(string message)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(message);
            var root = document.RootElement;
            return root.ValueKind == System.Text.Json.JsonValueKind.Object
                && root.TryGetProperty("kind", out var kind) && kind.GetString() == "window"
                && root.TryGetProperty("name", out var name) && name.GetString() == "exit";
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static int Run(string url, BridgeHost host)
    {
        try
        {
            var window = new PhotinoWindow()
                .SetTitle("Paddock Principal")
                .SetUseOsDefaultSize(false)
                .SetSize(1440, 900)
                .SetDevToolsEnabled(true)
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    var current = (PhotinoWindow)sender!;
                    if (IsExit(message))
                    {
                        current.Close();
                        return;
                    }

                    var exchange = host.Handle(message);
                    current.SendWebMessage(exchange.Response);
                    foreach (var pushed in exchange.Events)
                    {
                        current.SendWebMessage(pushed);
                    }
                })
                .Load(url);
            window.WaitForClose();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Photino failed: " + ex.GetType().Name + ": " + ex.Message);
            Console.Error.WriteLine("The WinForms + WebView2 fallback (TECH §1.1) is not built. Use --dev and a browser.");
            return 1;
        }
    }
}
