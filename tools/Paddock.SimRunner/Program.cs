namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args) =>
        args.Length > 0 && args[0] == I18nCheckCommand.Name
            ? I18nCheckCommand.Execute(args, Console.Out, Console.Error)
            : RngCommand.Execute(args, Console.Out, Console.Error);
}
