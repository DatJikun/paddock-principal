using Paddock.Domain.Time;

namespace Paddock.Domain.World;

/// <summary>
/// Persons, organizations, contracts, the id allocator, and the current date.
/// Updates return a new state. The previous value is unchanged, so keep the returned one.
/// Collections are enumerated in ordinal order of their canonical ids.
/// </summary>
public sealed partial class WorldState
{
    private readonly SortedDictionary<string, Person> _persons;
    private readonly SortedDictionary<string, Organization> _organizations;
    private readonly SortedDictionary<string, Contract> _contracts;
    private readonly SortedDictionary<string, PersonKnowledge> _knowledge;
    private readonly SortedDictionary<string, IWorldSection> _sections;

    private WorldState(
        GameDate currentDate,
        IdAllocator ids,
        SortedDictionary<string, Person> persons,
        SortedDictionary<string, Organization> organizations,
        SortedDictionary<string, Contract> contracts,
        SortedDictionary<string, PersonKnowledge> knowledge,
        SortedDictionary<string, IWorldSection> sections)
    {
        CurrentDate = currentDate;
        Ids = ids;
        _persons = persons;
        _organizations = organizations;
        _contracts = contracts;
        _knowledge = knowledge;
        _sections = sections;
    }

    public static WorldState At(GameDate currentDate) => new(
        currentDate,
        IdAllocator.Empty,
        new SortedDictionary<string, Person>(StringComparer.Ordinal),
        new SortedDictionary<string, Organization>(StringComparer.Ordinal),
        new SortedDictionary<string, Contract>(StringComparer.Ordinal),
        new SortedDictionary<string, PersonKnowledge>(StringComparer.Ordinal),
        new SortedDictionary<string, IWorldSection>(StringComparer.Ordinal));

    public GameDate CurrentDate { get; }

    public IdAllocator Ids { get; }

    public IReadOnlyList<Person> Persons => _persons.Values.ToArray();

    public IReadOnlyList<Organization> Organizations => _organizations.Values.ToArray();

    public IReadOnlyList<Contract> Contracts => _contracts.Values.ToArray();

    public IReadOnlyList<PersonKnowledge> Knowledge => _knowledge.Values.ToArray();

    /// <summary>The sections later systems keep in the world, in ordinal order of their names.</summary>
    public IReadOnlyList<IWorldSection> Sections => _sections.Values.ToArray();

    /// <summary>The section with that name, or null when the world has none.</summary>
    public IWorldSection? Section(string name)
    {
        SectionNames.Require(name, nameof(name));
        return _sections.TryGetValue(name, out var section) ? section : null;
    }

    /// <summary>The section with that name as <typeparamref name="T"/>, or null when the world has none.</summary>
    public T? Section<T>(string name)
        where T : class, IWorldSection
    {
        var section = Section(name);
        if (section is null)
        {
            return null;
        }

        return section as T
            ?? throw new InvalidOperationException($"Section '{name}' is a {section.GetType().Name}, not a {typeof(T).Name}.");
    }

    /// <summary>Adds the section, or replaces the one with the same name.</summary>
    public WorldState WithSection(IWorldSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        SectionNames.Require(section.Name, nameof(section));
        ArgumentOutOfRangeException.ThrowIfLessThan(section.SchemaVersion, 1);
        var sections = new SortedDictionary<string, IWorldSection>(_sections, StringComparer.Ordinal)
        {
            [section.Name] = section,
        };
        return new WorldState(CurrentDate, Ids, _persons, _organizations, _contracts, _knowledge, sections);
    }

    /// <summary>Drops the section. A world without sections hashes exactly as it did before sections existed.</summary>
    public WorldState WithoutSection(string name)
    {
        SectionNames.Require(name, nameof(name));
        if (!_sections.ContainsKey(name))
        {
            return this;
        }

        var sections = new SortedDictionary<string, IWorldSection>(_sections, StringComparer.Ordinal);
        sections.Remove(name);
        return new WorldState(CurrentDate, Ids, _persons, _organizations, _contracts, _knowledge, sections);
    }

    public WorldState WithDate(GameDate currentDate) =>
        new(currentDate, Ids, _persons, _organizations, _contracts, _knowledge, _sections);

    public Person GetPerson(PersonId id) => RequirePerson(id);

    public Organization GetOrganization(OrganizationId id) => RequireOrganization(id);

    public Contract GetContract(ContractId id) => RequireContract(id);

    public PersonTruth TruthOf(PersonId id) => RequirePerson(id).Truth;

    /// <summary>
    /// The organization's belief, or null when it has recorded none.
    /// Never fills the gap with <see cref="PersonTruth"/>.
    /// </summary>
    public PersonKnowledgeView? KnowledgeOf(OrganizationId observerId, PersonId subjectId)
    {
        RequireAssigned(observerId, subjectId);
        if (!_knowledge.TryGetValue(KnowledgeKey(observerId, subjectId), out var knowledge))
        {
            return null;
        }

        return new PersonKnowledgeView(knowledge);
    }

    public (WorldState State, PersonId Id) AddPerson(PersonSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var (ids, id) = spec.IsReal ? Ids.AdoptPerson(spec.RealId!) : Ids.AllocatePerson();
        var person = new Person(
            id,
            spec.GivenName,
            spec.FamilyName,
            spec.BirthDate,
            spec.Nationality,
            spec.IsReal,
            spec.Roles,
            spec.Truth);
        var persons = Clone(_persons);
        persons.Add(id.Value, person);
        return (new WorldState(CurrentDate, ids, persons, _organizations, _contracts, _knowledge, _sections), id);
    }

    /// <summary>
    /// Drops the person, their contracts, and beliefs about them. The id stays issued.
    /// </summary>
    public WorldState RemovePerson(PersonId id)
    {
        RequirePerson(id);
        var persons = Clone(_persons);
        persons.Remove(id.Value);
        var contracts = Clone(_contracts);
        foreach (var contract in _contracts.Values)
        {
            if (contract.PersonId == id)
            {
                contracts.Remove(contract.Id.Value);
            }
        }

        var knowledge = Clone(_knowledge);
        foreach (var belief in _knowledge)
        {
            if (belief.Value.SubjectId == id)
            {
                knowledge.Remove(belief.Key);
            }
        }

        return new WorldState(CurrentDate, Ids, persons, _organizations, contracts, knowledge, _sections);
    }

    public (WorldState State, OrganizationId Id) AddOrganization(OrganizationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var (ids, id) = spec.IsReal ? Ids.AdoptOrganization(spec.RealId!) : Ids.AllocateOrganization();
        var organization = new Organization(
            id,
            spec.Kind,
            spec.IsReal,
            spec.Founded,
            spec.Dissolved,
            spec.Budget,
            spec.Names,
            []);
        var organizations = Clone(_organizations);
        organizations.Add(id.Value, organization);
        return (new WorldState(CurrentDate, ids, _persons, organizations, _contracts, _knowledge, _sections), id);
    }

    /// <summary>
    /// Drops the organization and beliefs it holds. Refuses while a contract or lineage link still points at it.
    /// The id stays issued.
    /// </summary>
    public WorldState RemoveOrganization(OrganizationId id)
    {
        RequireOrganization(id);
        foreach (var contract in _contracts.Values)
        {
            if (contract.OrganizationId == id)
            {
                throw new InvalidOperationException($"Organization '{id}' still has contract '{contract.Id}'.");
            }
        }

        foreach (var organization in _organizations.Values)
        {
            foreach (var link in organization.Lineage)
            {
                if (link.OtherId == id)
                {
                    throw new InvalidOperationException($"Organization '{id}' is still linked from '{organization.Id}'.");
                }
            }
        }

        var organizations = Clone(_organizations);
        organizations.Remove(id.Value);
        var knowledge = Clone(_knowledge);
        foreach (var belief in _knowledge)
        {
            if (belief.Value.ObserverId == id)
            {
                knowledge.Remove(belief.Key);
            }
        }

        return new WorldState(CurrentDate, Ids, _persons, organizations, _contracts, knowledge, _sections);
    }

    public WorldState LinkLineage(OrganizationId predecessor, OrganizationId successor, GameDate from, GameDate? to)
    {
        if (!predecessor.IsAssigned || !successor.IsAssigned)
        {
            throw new ArgumentException("Lineage ids must be assigned.");
        }

        if (predecessor == successor)
        {
            throw new ArgumentException("An organization cannot link to itself.", nameof(successor));
        }

        var before = RequireOrganization(predecessor);
        var after = RequireOrganization(successor);
        if (One(before, LineageDirection.Successor) is not null || One(after, LineageDirection.Predecessor) is not null)
        {
            throw new InvalidOperationException("Lineage is a single chain, with at most one predecessor and one successor.");
        }

        if (Reaches(successor, predecessor))
        {
            throw new InvalidOperationException("That link would cycle the lineage.");
        }

        var organizations = Clone(_organizations);
        organizations[predecessor.Value] = before.WithLineage(Append(before.Lineage, new LineageLink(successor, LineageDirection.Successor, from, to)));
        organizations[successor.Value] = after.WithLineage(Append(after.Lineage, new LineageLink(predecessor, LineageDirection.Predecessor, from, to)));
        return new WorldState(CurrentDate, Ids, _persons, organizations, _contracts, _knowledge, _sections);
    }

    /// <summary>
    /// The chain that contains <paramref name="member"/>, from the earliest predecessor to the last successor.
    /// </summary>
    public IReadOnlyList<OrganizationId> LineageChain(OrganizationId member)
    {
        var start = member;
        var guard = 0;
        while (One(RequireOrganization(start), LineageDirection.Predecessor) is OrganizationId previous)
        {
            start = previous;
            if (++guard > _organizations.Count)
            {
                throw new InvalidOperationException("Lineage cycle.");
            }
        }

        var chain = new List<OrganizationId>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = start;
        while (true)
        {
            if (!seen.Add(current.Value))
            {
                throw new InvalidOperationException("Lineage cycle.");
            }

            chain.Add(current);
            if (One(RequireOrganization(current), LineageDirection.Successor) is not OrganizationId next)
            {
                return chain;
            }

            current = next;
        }
    }

    public (WorldState State, ContractId Id) AddContract(ContractSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        RequirePerson(spec.PersonId);
        RequireOrganization(spec.OrganizationId);
        var (ids, id) = Ids.AllocateContract();
        var contract = new Contract(
            id,
            spec.PersonId,
            spec.OrganizationId,
            spec.Role,
            spec.Start,
            spec.End,
            spec.Salary,
            spec.Exclusive,
            spec.Option,
            spec.ReleaseClause);
        foreach (var existing in _contracts.Values)
        {
            if (contract.ExclusivelyOverlaps(existing))
            {
                throw new InvalidOperationException(
                    $"Person '{spec.PersonId}' already has exclusive contract '{existing.Id}' overlapping this one.");
            }
        }

        var contracts = Clone(_contracts);
        contracts.Add(id.Value, contract);
        return (new WorldState(CurrentDate, ids, _persons, _organizations, contracts, _knowledge, _sections), id);
    }

    /// <summary>Drops the contract. The id stays issued.</summary>
    public WorldState RemoveContract(ContractId id)
    {
        RequireContract(id);
        var contracts = Clone(_contracts);
        contracts.Remove(id.Value);
        return new WorldState(CurrentDate, Ids, _persons, _organizations, contracts, _knowledge, _sections);
    }

    public WorldState SetKnowledge(PersonKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        RequireOrganization(knowledge.ObserverId);
        RequirePerson(knowledge.SubjectId);
        var beliefs = Clone(_knowledge);
        beliefs[KnowledgeKey(knowledge.ObserverId, knowledge.SubjectId)] = knowledge;
        return new WorldState(CurrentDate, Ids, _persons, _organizations, _contracts, beliefs, _sections);
    }

    private Person RequirePerson(PersonId id)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(id));
        }

        if (!_persons.TryGetValue(id.Value, out var person))
        {
            throw new InvalidOperationException($"Unknown person '{id}'.");
        }

        return person;
    }

    private Organization RequireOrganization(OrganizationId id)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(id));
        }

        if (!_organizations.TryGetValue(id.Value, out var organization))
        {
            throw new InvalidOperationException($"Unknown organization '{id}'.");
        }

        return organization;
    }

    private Contract RequireContract(ContractId id)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Contract id is unassigned.", nameof(id));
        }

        if (!_contracts.TryGetValue(id.Value, out var contract))
        {
            throw new InvalidOperationException($"Unknown contract '{id}'.");
        }

        return contract;
    }

    private bool Reaches(OrganizationId from, OrganizationId target)
    {
        var current = from;
        var guard = 0;
        while (true)
        {
            if (current == target)
            {
                return true;
            }

            if (One(RequireOrganization(current), LineageDirection.Successor) is not OrganizationId next)
            {
                return false;
            }

            current = next;
            if (++guard > _organizations.Count)
            {
                throw new InvalidOperationException("Lineage cycle.");
            }
        }
    }

    private static OrganizationId? One(Organization organization, LineageDirection direction)
    {
        OrganizationId? found = null;
        foreach (var link in organization.Lineage)
        {
            if (link.Direction != direction)
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException($"Organization '{organization.Id}' has more than one {direction}.");
            }

            found = link.OtherId;
        }

        return found;
    }

    private static void RequireAssigned(OrganizationId observerId, PersonId subjectId)
    {
        if (!observerId.IsAssigned)
        {
            throw new ArgumentException("Observer id is unassigned.", nameof(observerId));
        }

        if (!subjectId.IsAssigned)
        {
            throw new ArgumentException("Subject id is unassigned.", nameof(subjectId));
        }
    }

    private static string KnowledgeKey(OrganizationId observerId, PersonId subjectId) =>
        observerId.Value + "\u001f" + subjectId.Value;

    private static List<LineageLink> Append(IReadOnlyList<LineageLink> links, LineageLink link)
    {
        var copy = new List<LineageLink>(links.Count + 1);
        copy.AddRange(links);
        copy.Add(link);
        return copy;
    }

    private static SortedDictionary<string, T> Clone<T>(SortedDictionary<string, T> source) =>
        new(source, StringComparer.Ordinal);
}
