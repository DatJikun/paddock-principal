using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.SimRunner;

public static class RngCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], "rng", StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: rng --seed <ulong> --stream <name> --season <int> --count <int> [--round <int>]");
            return 1;
        }

        ulong? seed = null;
        string? stream = null;
        int? season = null;
        int? count = null;
        int? round = null;

        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--seed" or "--stream" or "--season" or "--count" or "--round"))
            {
                stderr.WriteLine($"Unknown argument: {flag}");
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine($"Missing value for {flag}.");
                return 1;
            }

            var value = args[++i];
            switch (flag)
            {
                case "--seed":
                    if (seed is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeed))
                    {
                        stderr.WriteLine($"Invalid --seed value: {value}");
                        return 1;
                    }

                    seed = parsedSeed;
                    break;
                case "--stream":
                    if (stream is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (value.Length == 0)
                    {
                        stderr.WriteLine("--stream must not be empty.");
                        return 1;
                    }

                    stream = value;
                    break;
                case "--season":
                    if (season is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedSeason))
                    {
                        stderr.WriteLine($"Invalid --season value: {value}");
                        return 1;
                    }

                    season = parsedSeason;
                    break;
                case "--count":
                    if (count is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCount) || parsedCount <= 0)
                    {
                        stderr.WriteLine("--count must be an integer greater than zero.");
                        return 1;
                    }

                    count = parsedCount;
                    break;
                case "--round":
                    if (round is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedRound))
                    {
                        stderr.WriteLine($"Invalid --round value: {value}");
                        return 1;
                    }

                    round = parsedRound;
                    break;
            }
        }

        if (seed is null || stream is null || season is null || count is null)
        {
            stderr.WriteLine("Missing required option. Expected --seed, --stream, --season and --count.");
            return 1;
        }

        var rng = RngStreams.Derive(seed.Value, stream, season.Value, round);
        for (var n = 0; n < count.Value; n++)
        {
            stdout.WriteLine(rng.NextULong().ToString(CultureInfo.InvariantCulture));
        }

        return 0;
    }

    private static int Duplicate(TextWriter stderr, string flag)
    {
        stderr.WriteLine($"Duplicate option: {flag}");
        return 1;
    }
}
