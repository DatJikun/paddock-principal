using System.Globalization;
using System.Text;
using Paddock.Domain.People;

namespace Paddock.Tests.People;

internal static class PeopleCanonical
{
    public static string Driver(GeneratedDriver driver)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        Field(builder, "id", driver.Id, first: true);
        Field(builder, "given", driver.GivenName);
        Field(builder, "family", driver.FamilyName);
        Field(builder, "nationality", driver.Nationality);
        Field(builder, "born", driver.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field(builder, "female", driver.IsFemale);
        Field(builder, "quality", driver.Quality.ToString());
        Field(builder, "era", driver.EraYear);
        Attributes(builder, "attributes", driver.Attributes);
        Attributes(builder, "potentialAttributes", driver.PotentialAttributes);
        Field(builder, "overall", driver.Overall);
        Field(builder, "potential", driver.Potential);
        Field(builder, "growthStart", driver.Curve.GrowthStartAge);
        Field(builder, "peak", driver.Curve.PeakAge);
        Field(builder, "plateau", driver.Curve.PlateauYears);
        Field(builder, "declineMilli", driver.Curve.DeclineMilliPerYear);
        Field(builder, "personality", driver.Personality.Primary.ToString());
        Field(builder, "loyalty", driver.Personality.Loyalty);
        Field(builder, "ambition", driver.Personality.Ambition);
        Field(builder, "temperament", driver.Personality.Temperament);
        Field(builder, "professionalism", driver.Personality.Professionalism);
        Field(builder, "ego", driver.Personality.Ego);
        builder.Append('}');
        return builder.ToString();
    }

    public static string Staff(GeneratedStaff staff)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        Field(builder, "id", staff.Id, first: true);
        Field(builder, "role", staff.Role.ToString());
        Field(builder, "given", staff.GivenName);
        Field(builder, "family", staff.FamilyName);
        Field(builder, "nationality", staff.Nationality);
        Field(builder, "born", staff.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field(builder, "female", staff.IsFemale);
        Field(builder, "quality", staff.Quality.ToString());
        Field(builder, "era", staff.EraYear);
        builder.Append(",\"attributes\":{");
        for (var i = 0; i < staff.Attributes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(staff.Attributes[i].Key);
            builder.Append("\":");
            builder.Append(staff.Attributes[i].Value.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append('}');
        Field(builder, "innovation", staff.Innovation);
        Field(builder, "ambition", staff.Ambition);
        Field(builder, "loyalty", staff.Loyalty);
        Field(builder, "growthStart", staff.Curve.GrowthStartAge);
        Field(builder, "peak", staff.Curve.PeakAge);
        Field(builder, "plateau", staff.Curve.PlateauYears);
        Field(builder, "declineMilli", staff.Curve.DeclineMilliPerYear);
        builder.Append('}');
        return builder.ToString();
    }

    private static void Attributes(StringBuilder builder, string name, DriverAttributes attributes)
    {
        builder.Append(",\"");
        builder.Append(name);
        builder.Append("\":{");
        for (var i = 0; i < GenerationEstimates.DriverAttributeKeys.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            string key = GenerationEstimates.DriverAttributeKeys[i];
            builder.Append('"');
            builder.Append(key);
            builder.Append("\":");
            builder.Append(attributes.Get(key).ToString(CultureInfo.InvariantCulture));
        }

        builder.Append('}');
    }

    private static void Field(StringBuilder builder, string name, string value, bool first = false)
    {
        if (!first)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(name);
        builder.Append("\":");
        AppendString(builder, value);
    }

    private static void Field(StringBuilder builder, string name, int value)
    {
        builder.Append(",\"");
        builder.Append(name);
        builder.Append("\":");
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Field(StringBuilder builder, string name, bool? value)
    {
        builder.Append(",\"");
        builder.Append(name);
        builder.Append("\":");
        builder.Append(value switch
        {
            true => "true",
            false => "false",
            null => "null",
        });
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
    }
}
