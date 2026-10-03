using System.Globalization;
using System.Text;
using Paddock.Data.Authored;
using Paddock.Domain.World;

namespace Paddock.Tests.Authored;

internal static class EraSamples
{
    public const string Catalog = """
        [
          {
            "id": "tobacco_advertising",
            "category": "commercial",
            "name": "Tobacco",
            "type": "enum",
            "values": ["unrestricted", "banned"],
            "unit": null,
            "gameplay_effect": "Tobacco sponsorship."
          }
        ]
        """;

    public const string Timeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Covers the whole window."
          }
        ]
        """;

    public const string UnknownDimensionTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Still valid."
          },
          {
            "dimension": "not_real",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Not in the era catalog."
          }
        ]
        """;

    public const string ValueListCatalog = """
        [
          {
            "id": "tobacco_advertising",
            "category": "commercial",
            "name": "Tobacco",
            "type": "enum",
            "values": ["unrestricted", "banned"],
            "unit": null,
            "gameplay_effect": "Tobacco sponsorship."
          },
          {
            "id": "hans_device",
            "category": "safety",
            "name": "HANS",
            "type": "bool",
            "values": ["none", "mandatory"],
            "unit": null,
            "gameplay_effect": "Head restraint."
          },
          {
            "id": "team_budget_low_nominal_usd",
            "category": "economic",
            "name": "Budget",
            "type": "number",
            "values": null,
            "unit": "usd",
            "gameplay_effect": "Backmarker budget."
          }
        ]
        """;

    public const string BadEnumTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "sometimes",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Enum value is not in the catalog list."
          },
          {
            "dimension": "hans_device",
            "value": "optional",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Bool value is not in the catalog list."
          },
          {
            "dimension": "team_budget_low_nominal_usd",
            "value": "20000",
            "from": 1950,
            "to": null,
            "confidence": "low",
            "source": "https://example.com/era",
            "notes": "Number dimensions are not checked against a value list."
          }
        ]
        """;

    public const string GapTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Stops at 1960."
          },
          {
            "dimension": "tobacco_advertising",
            "value": "banned",
            "from": 1962,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Leaves 1961 uncovered."
          }
        ]
        """;

    public const string OverlapTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Runs through 1960."
          },
          {
            "dimension": "tobacco_advertising",
            "value": "banned",
            "from": 1960,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Overlaps 1960."
          }
        ]
        """;

    public const string InvertedTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Covers the window."
          },
          {
            "dimension": "tobacco_advertising",
            "value": "banned",
            "from": 1970,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/era",
            "notes": "Ends before it starts."
          }
        ]
        """;

    public const string HttpSourceTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "http://example.com/tobacco",
            "notes": "Absolute, but not https."
          }
        ]
        """;

    public const string PlainSourceTimeline = """
        [
          {
            "dimension": "tobacco_advertising",
            "value": "unrestricted",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "fixture",
            "notes": "Not an absolute URL."
          }
        ]
        """;

    public const string UnknownPropertyCatalog = """
        [
          {
            "id": "tobacco_advertising",
            "category": "commercial",
            "name": "Tobacco",
            "type": "enum",
            "values": ["unrestricted", "banned"],
            "unit": null,
            "gameplay_effect": "Tobacco sponsorship.",
            "extra": true
          }
        ]
        """;

    public static List<(int Year, decimal Cpi, bool Partial, string Source)> ClosedCpi(bool includePartialFinalSeason = true)
    {
        var rows = new List<(int Year, decimal Cpi, bool Partial, string Source)>();
        for (var year = AuthoredDataValidator.FirstSeason; year <= CpiBook.BaseYear; year++)
        {
            rows.Add((year, year - 1949, false, "https://example.com/cpi"));
        }

        if (includePartialFinalSeason)
        {
            rows.Add((AuthoredDataValidator.LastSeason, 77m, true, "https://example.com/cpi"));
        }

        return rows;
    }

    public static string CpiJson(IReadOnlyList<(int Year, decimal Cpi, bool Partial, string Source)> rows)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var row = rows[i];
            builder.Append("{\"year\":");
            builder.Append(row.Year.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"cpi\":");
            builder.Append(row.Cpi.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"partial\":");
            builder.Append(row.Partial ? "true" : "false");
            builder.Append(",\"source\":\"");
            builder.Append(row.Source);
            builder.Append("\"}");
        }

        builder.Append(']');
        return builder.ToString();
    }
}
