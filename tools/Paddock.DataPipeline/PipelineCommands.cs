using System.Text.Json;

namespace Paddock.DataPipeline;

public static class PipelineCommands
{
    public const string Usage =
        """
        Usage:
          fetch --from <year> --to <year> [--cache <dir>] [--force]
          normalize [--cache <dir>]
          summary [--cache <dir>] [--from <year>] [--to <year>]
          stats [--cache <dir>] [--from <year>] [--to <year>]
        """;

    public static async Task<int> ExecuteAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        IJolpicaHttp? http = null,
        IJolpicaPacer? pacer = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0)
        {
            stderr.WriteLine(Usage);
            return 1;
        }

        try
        {
            switch (args[0])
            {
                case "fetch":
                    if (http is null || pacer is null)
                    {
                        throw new InvalidOperationException("Fetch requires an HTTP client and a pacer.");
                    }

                    return await FetchCommand.ExecuteAsync(
                        args,
                        stdout,
                        stderr,
                        http,
                        pacer,
                        delay ?? Task.Delay,
                        cancellationToken);
                case "normalize":
                    return NormalizeCommand.Execute(args, stdout, stderr);
                case "summary":
                    return SummaryCommand.Execute(args, stdout, stderr);
                case "stats":
                    return StatsCommand.Execute(args, stdout, stderr);
                default:
                    stderr.WriteLine($"Unknown command: {args[0]}");
                    stderr.WriteLine(Usage);
                    return 1;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }
}
