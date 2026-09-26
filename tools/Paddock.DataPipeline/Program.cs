namespace Paddock.DataPipeline;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "validate-authored", StringComparison.Ordinal))
        {
            return ValidateAuthoredCommand.Execute(args, Console.Out, Console.Error);
        }

        using var client = new HttpJolpicaClient();
        var pacer = SlidingWindowPacer.ForJolpica();
        return await PipelineCommands.ExecuteAsync(args, Console.Out, Console.Error, client, pacer);
    }
}
