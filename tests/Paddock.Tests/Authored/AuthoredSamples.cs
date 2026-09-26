namespace Paddock.Tests.Authored;

internal static class AuthoredSamples
{
    public const string Catalog = """
        [
          {
            "id": "points_scale",
            "category": "sporting",
            "name": "Points",
            "type": "enum",
            "values": ["a", "b"],
            "unit": null,
            "gameplay_effect": "Sets the points scale."
          }
        ]
        """;

    public const string Timeline = """
        [
          {
            "dimension": "points_scale",
            "value": "a",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Covers the whole window."
          }
        ]
        """;

    public const string Ideas = "[]";

    public const string Circuits = """
        {
          "notes": "fixture",
          "character_tags": [
            {
              "id": "street",
              "meaning": "Walls and little runoff."
            }
          ],
          "circuits": [
            {
              "circuit_id": "test",
              "name": "Test Circuit",
              "country": "GBR",
              "layouts": [
                {
                  "layout_id": "test_1950",
                  "years_used_in_f1": [1950],
                  "length_km": 5.0,
                  "corners": null,
                  "character": ["street"],
                  "profile_guess": {
                    "straights": 0.25,
                    "high_speed": 0.25,
                    "low_speed": 0.25,
                    "braking": 0.25
                  },
                  "confidence": "high",
                  "source": "https://example.com/fixture",
                  "notes": "fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string RaceMap = """
        [
          {
            "season": 1950,
            "round": 1,
            "layout_id": "test_1950"
          }
        ]
        """;

    public const string UnknownDimensionTimeline = """
        [
          {
            "dimension": "points_scale",
            "value": "a",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Still valid."
          },
          {
            "dimension": "not_real",
            "value": "a",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Not in the catalog."
          }
        ]
        """;

    public const string ValueListCatalog = """
        [
          {
            "id": "points_scale",
            "category": "sporting",
            "name": "Points",
            "type": "enum",
            "values": ["a", "b"],
            "unit": null,
            "gameplay_effect": "Sets the points scale."
          },
          {
            "id": "refuelling",
            "category": "sporting",
            "name": "Refuelling",
            "type": "bool",
            "values": ["allowed", "banned"],
            "unit": null,
            "gameplay_effect": "Allows or bans refuelling."
          }
        ]
        """;

    public const string BadEnumTimeline = """
        [
          {
            "dimension": "points_scale",
            "value": "nope",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Enum value is not in the catalog list."
          },
          {
            "dimension": "refuelling",
            "value": "sometimes",
            "from": 1950,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Bool value is not in the catalog list."
          }
        ]
        """;

    public const string GapOverlapTimeline = """
        [
          {
            "dimension": "points_scale",
            "value": "a",
            "from": 1950,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Stops at 1960."
          },
          {
            "dimension": "points_scale",
            "value": "b",
            "from": 1960,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Overlaps 1960."
          },
          {
            "dimension": "points_scale",
            "value": "a",
            "from": 1962,
            "to": null,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Leaves 1961 uncovered."
          },
          {
            "dimension": "points_scale",
            "value": "a",
            "from": 1970,
            "to": 1960,
            "confidence": "high",
            "source": "https://example.com/fixture",
            "notes": "Ends before it starts."
          }
        ]
        """;

    public const string BadProfileCircuits = """
        {
          "notes": "fixture",
          "character_tags": [
            {
              "id": "street",
              "meaning": "Walls and little runoff."
            }
          ],
          "circuits": [
            {
              "circuit_id": "test",
              "name": "Test Circuit",
              "country": "GBR",
              "layouts": [
                {
                  "layout_id": "test_1950",
                  "years_used_in_f1": [1950],
                  "length_km": 5.0,
                  "corners": null,
                  "character": ["street"],
                  "profile_guess": {
                    "straights": 0.5,
                    "high_speed": 0.5,
                    "low_speed": 0.5,
                    "braking": 0.5
                  },
                  "confidence": "high",
                  "source": "https://example.com/fixture",
                  "notes": "Weights sum to 2."
                }
              ]
            }
          ]
        }
        """;

    public const string MissingLayoutRaceMap = """
        [
          {
            "season": 1950,
            "round": 1,
            "layout_id": "missing_layout"
          }
        ]
        """;

    public const string UnknownPropertyCircuits = """
        {
          "notes": "fixture",
          "character_tags": [
            {
              "id": "street",
              "meaning": "Walls and little runoff."
            }
          ],
          "circuits": [
            {
              "circuit_id": "test",
              "name": "Test Circuit",
              "country": "GBR",
              "layouts": [
                {
                  "layout_id": "test_1950",
                  "years_used_in_f1": [1950],
                  "length_km": 5.0,
                  "corners": null,
                  "character": ["street"],
                  "profile_guess": {
                    "straights": 0.25,
                    "high_speed": 0.25,
                    "low_speed": 0.25,
                    "braking": 0.25,
                    "extra": 0.0
                  },
                  "confidence": "high",
                  "source": "https://example.com/fixture",
                  "notes": "Unknown profile property."
                }
              ]
            }
          ]
        }
        """;
}
