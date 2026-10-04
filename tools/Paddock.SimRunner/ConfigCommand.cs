using System.Globalization;
using Paddock.Domain.Career;

namespace Paddock.SimRunner;

public static class ConfigCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], "config", StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: config --preset <name> [--axis <name>=<value> ...]");
            return 1;
        }

        string? presetName = null;
        var axes = new List<(string Name, string Value)>();
        var seenAxes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--preset" or "--axis"))
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
                case "--preset":
                    if (presetName is not null)
                    {
                        return Duplicate(stderr, flag);
                    }

                    presetName = value;
                    break;
                case "--axis":
                    var split = value.Split('=', 2);
                    if (split.Length != 2 || split[0].Length == 0)
                    {
                        stderr.WriteLine($"Invalid --axis value: {value}");
                        return 1;
                    }

                    if (!seenAxes.Add(split[0]))
                    {
                        stderr.WriteLine($"Duplicate axis: {split[0]}");
                        return 1;
                    }

                    axes.Add((split[0], split[1]));
                    break;
            }
        }

        if (presetName is null)
        {
            stderr.WriteLine("Missing required option. Expected --preset.");
            return 1;
        }

        if (!PresetArgument.TryParse(presetName, out var preset))
        {
            stderr.WriteLine(PresetArgument.Invalid(presetName));
            return 1;
        }

        var config = CareerConfig.FromPreset(preset);
        foreach (var (name, value) in axes)
        {
            if (!TryApply(config, name, value, stderr, out config))
            {
                return 1;
            }
        }

        var validation = config.Validate();
        stdout.WriteLine(config.ToCanonicalJson());
        foreach (var error in validation.Errors)
        {
            stderr.WriteLine(FormatIssue("error", error));
        }

        foreach (var warning in validation.Warnings)
        {
            stderr.WriteLine(FormatIssue("warning", warning));
        }

        return validation.IsValid ? 0 : 1;
    }

    private static bool TryApply(
        CareerConfig config,
        string name,
        string value,
        TextWriter stderr,
        out CareerConfig updated)
    {
        updated = config;
        switch (name)
        {
            case "people":
                if (!TryNamed(value, out PeopleSource people))
                {
                    stderr.WriteLine($"Invalid --axis people value: {value}");
                    return false;
                }

                updated = config.WithPeopleSource(people);
                return true;
            case "rules":
                if (!TryNamed(value, out RulesSource rules))
                {
                    stderr.WriteLine($"Invalid --axis rules value: {value}");
                    return false;
                }

                updated = config.WithRulesSource(rules);
                return true;
            case "ai":
                if (!TryNamed(value, out AiBehavior ai))
                {
                    stderr.WriteLine($"Invalid --axis ai value: {value}");
                    return false;
                }

                updated = config.WithAiBehavior(ai);
                return true;
            case "history-strength":
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var historyStrength))
                {
                    stderr.WriteLine($"Invalid --axis history-strength value: {value}");
                    return false;
                }

                updated = config.WithHistoryStrength(historyStrength);
                return true;
            case "randomness":
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var randomness))
                {
                    stderr.WriteLine($"Invalid --axis randomness value: {value}");
                    return false;
                }

                updated = config.WithRandomnessLevel(randomness);
                return true;
            case "fatality":
                if (!TryNamed(value, out FatalityLevel fatality))
                {
                    stderr.WriteLine($"Invalid --axis fatality value: {value}");
                    return false;
                }

                updated = config.WithFatalityLevel(fatality);
                return true;
            case "start-year":
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var startYear))
                {
                    stderr.WriteLine($"Invalid --axis start-year value: {value}");
                    return false;
                }

                updated = config.WithStartYear(startYear);
                return true;
            case "team":
                updated = config.WithPlayerTeam(value);
                return true;
            case "no-numbers":
                if (value == "true")
                {
                    updated = config.WithNoNumbers(true);
                    return true;
                }

                if (value == "false")
                {
                    updated = config.WithNoNumbers(false);
                    return true;
                }

                stderr.WriteLine($"Invalid --axis no-numbers value: {value}");
                return false;
            default:
                stderr.WriteLine($"Unknown axis: {name}");
                return false;
        }
    }

    private static bool TryNamed<TEnum>(string value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
            {
                parsed = candidate;
                return true;
            }
        }

        parsed = default;
        return false;
    }

    private static string FormatIssue(string kind, CareerConfigIssue issue)
    {
        if (issue.Arguments.Count == 0)
        {
            return kind + " " + issue.Code;
        }

        return kind + " " + issue.Code + " " + string.Join(' ', issue.Arguments);
    }

    private static int Duplicate(TextWriter stderr, string flag)
    {
        stderr.WriteLine($"Duplicate option: {flag}");
        return 1;
    }
}
