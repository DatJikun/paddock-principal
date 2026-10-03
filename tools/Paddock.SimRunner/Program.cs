namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "vote-sim", StringComparison.Ordinal)
            ? VoteSimCommand.Execute(args, Console.Out, Console.Error)
            : RngCommand.Execute(args, Console.Out, Console.Error);
}
