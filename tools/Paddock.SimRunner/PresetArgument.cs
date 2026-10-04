using Paddock.Domain.Career;

namespace Paddock.SimRunner;

/// <summary>Parses <c>--preset</c> the same way in every command and names the valid values when the input is wrong.</summary>
internal static class PresetArgument
{
    /// <summary>The presets a command accepts, in declaration order. <see cref="CareerPreset.Custom"/> is built with axes, not named.</summary>
    public static string ValidNames { get; } = string.Join(
        ", ",
        Enum.GetValues<CareerPreset>().Where(preset => preset != CareerPreset.Custom).Select(preset => preset.ToString()));

    public static bool TryParse(string name, out CareerPreset preset)
    {
        foreach (var candidate in Enum.GetValues<CareerPreset>())
        {
            if (candidate != CareerPreset.Custom && string.Equals(candidate.ToString(), name, StringComparison.Ordinal))
            {
                preset = candidate;
                return true;
            }
        }

        preset = default;
        return false;
    }

    public static string Invalid(string name) => "Invalid --preset value: " + name + ". Expected one of: " + ValidNames + ".";
}
