namespace Paddock.Tests.Authored;

internal static class AuthoredWorldSamples
{
    public const string Technologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": "cooper",
              "car": "Cooper",
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string Engines = """
        {
          "notes": "fixture",
          "supply_types": [
            { "id": "works", "meaning": "Works" },
            { "id": "customer", "meaning": "Customer" },
            { "id": "partner", "meaning": "Partner" },
            { "id": "badged", "meaning": "Badged" },
            { "id": "unknown", "meaning": "Unknown" }
          ],
          "entries": [
            {
              "constructorId": "cooper",
              "year": 1950,
              "supplier": "JAP",
              "engine_name": "JAP",
              "type": "customer",
              "confidence": "high",
              "source": "https://example.com/fixture"
            }
          ]
        }
        """;

    public const string Lineage = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "cooper-line",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1950,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string Founders = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "cooper",
              "season_count": 1,
              "from": 1950,
              "to": 1950,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1950, "to": 1950 }
              ],
              "founded": 1950,
              "founders": ["John Cooper"],
              "country": "GBR",
              "base_city": "Surbiton",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string Staff = """
        [
          {
            "id": "john_cooper",
            "name": "John Cooper",
            "born": "1920-01-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "cooper",
                "role": "designer",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string DuplicateTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          },
          {
            "id": "rear_engine",
            "name": "Rear engine again",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": null,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string BadAreaTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "wings",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string UnknownPrerequisiteTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": ["missing"],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string CyclicTechnologies = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "area": "aero",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": ["beta"],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          },
          {
            "id": "beta",
            "name": "Beta",
            "area": "aero",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": ["alpha"],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string SeasonOrderTechnologies = """
        [
          {
            "id": "late",
            "name": "Late",
            "area": "chassis",
            "first_used": {
              "season": 1960,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1970,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          },
          {
            "id": "gap",
            "name": "Gap",
            "area": "chassis",
            "first_used": {
              "season": null,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 2000,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 2010,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string InvertedBanTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [
              {
                "from": 1980,
                "to": 1970,
                "note": "fixture",
                "source": "https://example.com/fixture"
              }
            ],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string UnknownTeamTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": "not_a_team",
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string BadSourceTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "first_used": {
              "season": 1950,
              "team": "cooper",
              "car": null,
              "source": "http://example.com/nope",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string UnknownPropertyTechnologies = """
        [
          {
            "id": "rear_engine",
            "name": "Rear engine",
            "area": "chassis",
            "extra": true,
            "first_used": {
              "season": 1950,
              "team": null,
              "car": null,
              "source": "https://example.com/fixture",
              "note": "fixture"
            },
            "widespread_by": 1950,
            "banned": [],
            "prerequisites": [],
            "earliest_plausible": {
              "season": 1950,
              "reason": "fixture",
              "source": "https://example.com/fixture"
            },
            "effect_summary": "fixture",
            "confidence": "high"
          }
        ]
        """;

    public const string EngineYearTooEarly = """
        {
          "notes": "fixture",
          "supply_types": [
            { "id": "works", "meaning": "Works" },
            { "id": "customer", "meaning": "Customer" },
            { "id": "partner", "meaning": "Partner" },
            { "id": "badged", "meaning": "Badged" },
            { "id": "unknown", "meaning": "Unknown" }
          ],
          "entries": [
            {
              "constructorId": "cooper",
              "year": 1949,
              "supplier": "JAP",
              "engine_name": "JAP",
              "type": "customer",
              "confidence": "high",
              "source": "https://example.com/fixture"
            }
          ]
        }
        """;

    public const string EngineTypeUnknown = """
        {
          "notes": "fixture",
          "supply_types": [
            { "id": "works", "meaning": "Works" },
            { "id": "customer", "meaning": "Customer" },
            { "id": "partner", "meaning": "Partner" },
            { "id": "badged", "meaning": "Badged" },
            { "id": "unknown", "meaning": "Unknown" }
          ],
          "entries": [
            {
              "constructorId": "cooper",
              "year": 1950,
              "supplier": "JAP",
              "engine_name": "JAP",
              "type": "nope",
              "confidence": "high",
              "source": "https://example.com/fixture"
            }
          ]
        }
        """;

    public const string EngineEmptySupplier = """
        {
          "notes": "fixture",
          "supply_types": [
            { "id": "works", "meaning": "Works" },
            { "id": "customer", "meaning": "Customer" },
            { "id": "partner", "meaning": "Partner" },
            { "id": "badged", "meaning": "Badged" },
            { "id": "unknown", "meaning": "Unknown" }
          ],
          "entries": [
            {
              "constructorId": "cooper",
              "year": 1950,
              "supplier": "",
              "engine_name": "JAP",
              "type": "customer",
              "confidence": "high",
              "source": "https://example.com/fixture"
            }
          ]
        }
        """;

    public const string DuplicateLineage = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1950,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            },
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1951,
                  "to": 1951,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string HandoverLineage = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1952,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                },
                {
                  "constructorId": "cooper",
                  "from": 1952,
                  "to": 1954,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string OverlappingLineage = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1953,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                },
                {
                  "constructorId": "cooper",
                  "from": 1952,
                  "to": 1954,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string UnknownLineageConstructor = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "not_a_team",
                  "from": 1950,
                  "to": 1950,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string SharedConstructorYear = """
        {
          "notes": "fixture",
          "lineages": [
            {
              "lineage_id": "alpha",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1950,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            },
            {
              "lineage_id": "beta",
              "entries": [
                {
                  "constructorId": "cooper",
                  "from": 1950,
                  "to": 1950,
                  "how_it_ended": null,
                  "source": "https://example.com/fixture"
                }
              ]
            }
          ]
        }
        """;

    public const string DuplicateFounders = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1950,
              "to": 1950,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1950, "to": 1950 }
              ],
              "founded": 1950,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            },
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1951,
              "to": 1951,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1951, "to": 1951 }
              ],
              "founded": 1951,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string UnknownFounderConstructor = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1950,
              "to": 1950,
              "constructor_entries": [
                { "constructorId": "not_a_team", "from": 1950, "to": 1950 }
              ],
              "founded": 1950,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string InvertedFounderSpan = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1950,
              "to": 1950,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1960, "to": 1950 }
              ],
              "founded": 1940,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string FoundedAfterEntry = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1950,
              "to": 1955,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1950, "to": 1955 }
              ],
              "founded": 1960,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string DuplicateStaff = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "born": "1920-01-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "cooper",
                "role": "designer",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          },
          {
            "id": "alpha",
            "name": "Alpha again",
            "born": null,
            "nationality": null,
            "career": [
              {
                "org": "cooper",
                "role": "owner",
                "from": 1951,
                "to": 1951,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string BadBornStaff = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "born": "1920-13-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "cooper",
                "role": "designer",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string InvertedCareerStaff = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "born": "1920-01-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "cooper",
                "role": "designer",
                "from": 1960,
                "to": 1950,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string BadRoleStaff = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "born": "1920-01-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "cooper",
                "role": "mechanic",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string UnknownStaffOrg = """
        [
          {
            "id": "alpha",
            "name": "Alpha",
            "born": "1920-01-01",
            "nationality": "GBR",
            "career": [
              {
                "org": "not_a_team",
                "role": "designer",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture"
              },
              {
                "org": "not_a_team",
                "role": "designer",
                "from": 1950,
                "to": 1950,
                "source": "https://example.com/fixture",
                "series": "other"
              }
            ],
            "notable": ["fixture"],
            "confidence": "high"
          }
        ]
        """;

    public const string ConstructorSharedAcrossOrganizations = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 1,
              "from": 1950,
              "to": 1950,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1950, "to": 1950 }
              ],
              "founded": 1950,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            },
            {
              "organization_id": "beta",
              "season_count": 1,
              "from": 1951,
              "to": 1951,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1951, "to": 1951 }
              ],
              "founded": 1951,
              "founders": ["B"],
              "country": "GBR",
              "base_city": "B",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;

    public const string ConstructorYearOnTwoOrganizations = """
        {
          "notes": "fixture",
          "organizations": [
            {
              "organization_id": "alpha",
              "season_count": 3,
              "from": 1950,
              "to": 1952,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1950, "to": 1952 }
              ],
              "founded": 1950,
              "founders": ["A"],
              "country": "GBR",
              "base_city": "A",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            },
            {
              "organization_id": "beta",
              "season_count": 3,
              "from": 1952,
              "to": 1954,
              "constructor_entries": [
                { "constructorId": "cooper", "from": 1952, "to": 1954 }
              ],
              "founded": 1950,
              "founders": ["B"],
              "country": "GBR",
              "base_city": "B",
              "confidence": "high",
              "source": "https://example.com/fixture",
              "notes": "fixture"
            }
          ]
        }
        """;
}
