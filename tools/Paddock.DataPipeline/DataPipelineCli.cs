namespace Paddock.DataPipeline;

/// <summary>One command line of <c>DataPipeline --help</c>: the name, the flags, and one sentence.</summary>
public sealed record DataPipelineCommandHelp(string Name, string Usage, string Summary);

/// <summary><c>--help</c> and <c>help</c> for DataPipeline. Every command the program dispatches is listed here.</summary>
public static class DataPipelineCli
{
    public static IReadOnlyList<DataPipelineCommandHelp> Commands { get; } =
    [
        new("validate-authored", "validate-authored [--data-root <path>] [--with-cache [path]]", "Checks the authored data files and, optionally, the local cache."),
        new("fetch", "fetch --from <year> --to <year> [--cache <dir>] [--force]", "Downloads Jolpica pages into the local cache."),
        new("normalize", "normalize [--cache <dir>]", "Turns the cached Jolpica pages into normalized tables."),
        new("summary", "summary [--cache <dir>] [--from <year>] [--to <year>]", "Prints a season-by-season summary of the normalized cache."),
        new("stats", "stats [--cache <dir>] [--from <year>] [--to <year>]", "Prints aggregate statistics of the normalized cache."),
        new("ratings", "ratings [--cache <dir>] [--from <year>] [--to <year>] [--w-race <float>] [--w-quali <float>] [--lambda-time <float>] [--lambda-0 <float>]", "Fits driver and car ratings from the normalized results."),
        new("schedule", "schedule [--cache <dir>] [--pool-lead-years <years>]", "Builds the people schedule from the normalized cache."),
    ];

    public static bool IsHelp(string token) =>
        token is "help" or "--help" or "-h";

    public static void WriteHelp(TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        stdout.WriteLine("DataPipeline commands:");
        foreach (var command in Commands)
        {
            stdout.WriteLine(command.Name + " — " + command.Summary);
            stdout.WriteLine("    " + command.Usage);
        }
    }
}
