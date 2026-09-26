namespace Paddock.DataPipeline;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var client = new HttpJolpicaClient();
        var pacer = SlidingWindowPacer.ForJolpica();
        return await PipelineCommands.ExecuteAsync(args, Console.Out, Console.Error, client, pacer);
    }
}
