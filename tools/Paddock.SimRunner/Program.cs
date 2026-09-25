namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args) => RngCommand.Execute(args, Console.Out, Console.Error);
}
