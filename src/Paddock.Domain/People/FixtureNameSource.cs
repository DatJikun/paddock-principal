using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.Domain.People;

/// <summary>
/// Tiny in-memory name table used until R12 (<c>data/authored/names/names.json</c>) is merged.
/// Weights and the era split are estimates, written here so they can be replaced by that file.
/// </summary>
public sealed class FixtureNameSource : INameSource
{
    public const int EraSplitYear = 1950;
    public const string OtherNationality = "OTHER";

    private readonly record struct WeightedName(string Name, int Weight);

    private readonly Dictionary<string, WeightedName[][]> _male;
    private readonly Dictionary<string, WeightedName[][]> _female;
    private readonly Dictionary<string, WeightedName[][]> _family;

    public FixtureNameSource()
    {
        _male = new Dictionary<string, WeightedName[][]>(StringComparer.Ordinal);
        _female = new Dictionary<string, WeightedName[][]>(StringComparer.Ordinal);
        _family = new Dictionary<string, WeightedName[][]>(StringComparer.Ordinal);
        Add("GBR", "John*5,Peter*3,James*2", "Oliver*5,Jack*3,Harry*2", "Mary*5,Margaret*3,Joan*2", "Olivia*5,Emily*3,Sophie*2", "Taylor*4,Walker*3,Clarke*2", "Wright*4,Hughes*3,Bennett*2");
        Add("ITA", "Giuseppe*4,Mario*3,Franco*2", "Luca*5,Marco*3,Andrea*2", "Maria*5,Anna*3,Giovanna*2", "Giulia*5,Francesca*3,Chiara*2", "Rossi*5,Bianchi*3,Ferrari*2", "Russo*4,Romano*3,Colombo*2");
        Add("DEU", "Hans*5,Karl*3,Werner*2", "Lukas*4,Finn*3,Jonas*2", "Helga*4,Ursula*3,Ingrid*2", "Mia*5,Hanna*3,Lena*2", "Müller*5,Schmidt*4,Weber*2", "Becker*4,Hoffmann*3,Schäfer*2");
        Add("FRA", "Jean*5,Pierre*3,Henri*2", "Louis*4,Hugo*3,Adam*2", "Marie*5,Jeanne*3,Suzanne*2", "Emma*5,Léa*3,Chloé*2", "Martin*5,Bernard*3,Dubois*2", "Petit*4,Moreau*3,Laurent*2");
        Add("BRA", "José*4,Carlos*3,Paulo*2", "Miguel*4,Enzo*3,Pedro*2", "Ana*5,Maria*3,Lucia*2", "Alice*4,Helena*3,Laura*2", "Silva*5,Santos*4,Oliveira*2", "Souza*4,Lima*3,Costa*2");
        Add("USA", "Robert*4,William*3,Charles*2", "Liam*4,Noah*3,Ethan*2", "Dorothy*4,Betty*3,Helen*2", "Emma*4,Ava*3,Mia*2", "Smith*5,Johnson*4,Brown*2", "Miller*4,Davis*3,Garcia*2");
        Add(OtherNationality, "Alex*4,Niko*3,Sam*2", "Ari*4,Noa*3,Kai*2", "Noor*4,Eva*3,Iris*2", "Maya*4,Ines*3,Noor*2", "Novak*4,Keller*3,Berg*2", "Rahman*4,Costa*3,Berg*2");
        Nationalities = ["GBR", "ITA", "DEU", "FRA", "BRA", "USA", OtherNationality];
    }

    public IReadOnlyList<string> Nationalities { get; }

    public PersonName Pick(Xoshiro256StarStar rng, string nationality, int birthYear, bool female)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var code = Normalize(nationality);
        if (!_male.ContainsKey(code))
        {
            code = OtherNationality;
        }

        int period = birthYear < EraSplitYear ? 0 : 1;
        WeightedName[] givenPool = (female ? _female : _male)[code][period];
        WeightedName[] familyPool = _family[code][period];
        return new PersonName(PickWeighted(rng, givenPool), PickWeighted(rng, familyPool));
    }

    public IReadOnlyList<string> GivenNames(string nationality, int birthYear, bool female)
    {
        var code = Resolve(nationality);
        int period = birthYear < EraSplitYear ? 0 : 1;
        WeightedName[] pool = (female ? _female : _male)[code][period];
        return pool.Select(entry => entry.Name).ToArray();
    }

    public IReadOnlyList<string> FamilyNames(string nationality, int birthYear)
    {
        var code = Resolve(nationality);
        int period = birthYear < EraSplitYear ? 0 : 1;
        return _family[code][period].Select(entry => entry.Name).ToArray();
    }

    private string Resolve(string nationality)
    {
        var code = Normalize(nationality);
        return _male.ContainsKey(code) ? code : OtherNationality;
    }

    private static string Normalize(string nationality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nationality);
        return nationality.Trim().ToUpperInvariant();
    }

    private void Add(
        string nationality,
        string maleEarly,
        string maleLate,
        string femaleEarly,
        string femaleLate,
        string familyEarly,
        string familyLate)
    {
        _male[nationality] = [Parse(maleEarly), Parse(maleLate)];
        _female[nationality] = [Parse(femaleEarly), Parse(femaleLate)];
        _family[nationality] = [Parse(familyEarly), Parse(familyLate)];
    }

    private static WeightedName[] Parse(string line)
    {
        string[] parts = line.Split(',');
        var names = new WeightedName[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            string piece = parts[i];
            int star = piece.IndexOf('*');
            if (star <= 0 || star == piece.Length - 1)
            {
                throw new InvalidOperationException("Name fixture entries must look like Name*weight.");
            }

            string name = piece[..star];
            if (!int.TryParse(piece[(star + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int weight) || weight <= 0)
            {
                throw new InvalidOperationException("Name fixture weights must be positive integers.");
            }

            names[i] = new WeightedName(name, weight);
        }

        return names;
    }

    private static string PickWeighted(Xoshiro256StarStar rng, WeightedName[] pool)
    {
        int total = 0;
        foreach (WeightedName entry in pool)
        {
            total += entry.Weight;
        }

        int roll = rng.NextInt(0, total);
        int cursor = 0;
        foreach (WeightedName entry in pool)
        {
            cursor += entry.Weight;
            if (roll < cursor)
            {
                return entry.Name;
            }
        }

        throw new InvalidOperationException("Name weights did not cover the roll.");
    }
}
