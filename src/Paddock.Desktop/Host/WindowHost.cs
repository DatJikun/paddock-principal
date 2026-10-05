using Photino.NET;
using Paddock.Desktop.Bridge;

namespace Paddock.Desktop;

/// <summary>Photino window. The page talks to the bridge through the web message channel (TECH §1.2).</summary>
internal static class WindowHost
{
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
                    var exchange = host.Handle(message);
                    var current = (PhotinoWindow)sender!;
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
