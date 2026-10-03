using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Random;

namespace Paddock.SimRunner;

public static class GenPeopleCommand
{
    public const string RatingExplanationKey = "people.gen.rating_explanation";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], "gen-people", StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: gen-people --seed <ulong> --count <int> --season <int> [--lang en|pl]");
            return 1;
        }

        ulong? seed = null;
        int? count = null;
        int? season = null;
        string? language = null;

        for (var i = 1; i < args.Length; i++)
        {
            string flag = args[i];
            if (flag is not ("--seed" or "--count" or "--season" or "--lang"))
            {
                stderr.WriteLine("Unknown argument: " + flag);
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine("Missing value for " + flag + ".");
                return 1;
            }

            string value = args[++i];
            switch (flag)
            {
                case "--seed":
                    if (seed is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedSeed))
                    {
                        stderr.WriteLine("Invalid --seed value: " + value);
                        return 1;
                    }

                    seed = parsedSeed;
                    break;
                case "--count":
                    if (count is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedCount) || parsedCount <= 0)
                    {
                        stderr.WriteLine("--count must be an integer greater than zero.");
                        return 1;
                    }

                    count = parsedCount;
                    break;
                case "--season":
                    if (season is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedSeason)
                        || parsedSeason < GenerationEstimates.MinSeason
                        || parsedSeason > GenerationEstimates.MaxSeason)
                    {
                        stderr.WriteLine("Invalid --season value: " + value);
                        return 1;
                    }

                    season = parsedSeason;
                    break;
                case "--lang":
                    if (language is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    if (value is not ("en" or "pl"))
                    {
                        stderr.WriteLine("--lang must be en or pl.");
                        return 1;
                    }

                    language = value;
                    break;
            }
        }

        if (seed is null || count is null || season is null)
        {
            stderr.WriteLine("Missing required option. Expected --seed, --count and --season.");
            return 1;
        }

        IReadOnlyDictionary<string, string> strings = StringTable.Load(language ?? "en");
        string explanation = strings.Required(RatingExplanationKey)
            .Replace(
                "{starThreshold}",
                GenerationEstimates.StarPotential.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        stdout.WriteLine(explanation);
        stdout.WriteLine(string.Join(
            '\t',
            strings.Required("people.gen.column.id"),
            strings.Required("people.gen.column.name"),
            strings.Required("people.gen.column.nationality"),
            strings.Required("people.gen.column.born"),
            strings.Required("people.gen.column.overall"),
            strings.Required("people.gen.column.potential"),
            strings.Required("people.gen.column.personality")));

        var names = new FixtureNameSource();
        var generator = new DriverGenerator(new StableIdAllocator(), names);
        RngStream people = RngStream.Derive(seed.Value, RngStreamName.People, season.Value);
        GenerationRequest request = GenerationRequest.ForReview(season.Value, names.Nationalities);
        for (var n = 0; n < count.Value; n++)
        {
            GeneratedDriver driver = generator.Generate(people, season.Value, request);
            stdout.WriteLine(string.Join(
                '\t',
                driver.Id,
                driver.Name,
                driver.Nationality,
                driver.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                driver.Overall.ToString(CultureInfo.InvariantCulture),
                driver.Potential.ToString(CultureInfo.InvariantCulture),
                strings.Required(PersonalityKey(driver.Personality.Primary))));
        }

        return 0;
    }

    public static string PersonalityKey(PrimaryPersonality personality)
    {
        return personality switch
        {
            PrimaryPersonality.SeeksSecurity => "people.personality.seeks_security",
            PrimaryPersonality.Mercenary => "people.personality.mercenary",
            PrimaryPersonality.Loyal => "people.personality.loyal",
            PrimaryPersonality.Prestige => "people.personality.prestige",
            PrimaryPersonality.ShortTerm => "people.personality.short_term",
            PrimaryPersonality.Ambitious => "people.personality.ambitious",
            PrimaryPersonality.Mentor => "people.personality.mentor",
            PrimaryPersonality.TeamPlayer => "people.personality.team_player",
            _ => throw new ArgumentOutOfRangeException(nameof(personality), personality, "Unknown personality."),
        };
    }

    private static int Duplicate(TextWriter stderr, string flag)
    {
        stderr.WriteLine("Duplicate option: " + flag);
        return 1;
    }
}
