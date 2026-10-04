using System.Globalization;
using System.Text;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>
/// The talent pool as the world section named <see cref="SectionName"/> (PP-018, DESIGN 2.1): who is in it, which junior
/// seasons are paid for, what each organization's scouts concentrate on and how much each has observed, and the careers that
/// lapsed. The people themselves are ordinary persons of the world; this section only says who is currently in the pool.
/// Observed bands are not here: they are <see cref="PersonKnowledge"/> of the world, which is where T15 keeps beliefs.
/// <para>
/// Immutable: every change returns a new section. This type holds truth (it knows which members are real because the
/// person it points at is), so a manager reads it only through the pool query. The handle of a member comes from this section's
/// own counter, which only moves forward, so a handle is never reused (INV-009).
/// </para>
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash:
/// <code>
/// next &lt;nextHandle&gt;
/// members &lt;count&gt;
/// member &lt;len&gt;:&lt;personId&gt; &lt;handle&gt; &lt;len&gt;:&lt;entered&gt;
/// funding &lt;0|1&gt;
/// funder &lt;len&gt;:&lt;organizationId&gt;        (only when funding is 1)
/// programme &lt;CheapSlow|ExpensiveFast&gt;       (only when funding is 1)
/// season &lt;int&gt;                              (only when funding is 1)
/// lapsed &lt;count&gt;
/// lapse &lt;len&gt;:&lt;personId&gt; &lt;len&gt;:&lt;date&gt;
/// focus &lt;count&gt;
/// focus &lt;len&gt;:&lt;organizationId&gt; &lt;Pool|Person&gt; &lt;len&gt;:&lt;personId or -&gt;
/// observations &lt;count&gt;
/// observation &lt;len&gt;:&lt;organizationId&gt; &lt;len&gt;:&lt;personId&gt; &lt;milliPoints&gt;
/// </code>
/// </para>
/// </summary>
public sealed class TalentPoolSection : IWorldSection
{
    public const string SectionName = "talent-pool";

    public const string HandlePrefix = "talent-";

    private readonly SortedDictionary<string, PoolMember> _members;
    private readonly SortedDictionary<string, LapsedCareer> _lapsed;
    private readonly SortedDictionary<string, ScoutFocus> _focus;
    private readonly SortedDictionary<string, ObservationRow> _observations;

    private TalentPoolSection(
        long nextHandle,
        SortedDictionary<string, PoolMember> members,
        SortedDictionary<string, LapsedCareer> lapsed,
        SortedDictionary<string, ScoutFocus> focus,
        SortedDictionary<string, ObservationRow> observations)
    {
        NextHandle = nextHandle;
        _members = members;
        _lapsed = lapsed;
        _focus = focus;
        _observations = observations;
    }

    public static TalentPoolSection Empty { get; } = new(1, New<PoolMember>(), New<LapsedCareer>(), New<ScoutFocus>(), New<ObservationRow>());

    public string Name => SectionName;

    public int SchemaVersion => 1;

    /// <summary>The number the next member gets. Starts at 1.</summary>
    public long NextHandle { get; }

    /// <summary>The members, in order of person id.</summary>
    public IReadOnlyList<PoolMember> Members => _members.Values.ToArray();

    public int Count => _members.Count;

    public IReadOnlyList<LapsedCareer> Lapsed => _lapsed.Values.ToArray();

    public IReadOnlyList<ScoutFocus> Focuses => _focus.Values.ToArray();

    /// <summary>Every (organization, person, thousandths of observation points) the scouts have accumulated.</summary>
    public IReadOnlyList<ObservationRow> Observations => _observations.Values.ToArray();

    public static string HandleOf(long number) => HandlePrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Rebuilds a section from stored rows and checks them. Every handle must be below <paramref name="nextHandle"/>.</summary>
    public static TalentPoolSection Restore(
        long nextHandle,
        IEnumerable<PoolMember> members,
        IEnumerable<LapsedCareer> lapsed,
        IEnumerable<ScoutFocus> focuses,
        IEnumerable<ObservationRow> observations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextHandle, 1);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(lapsed);
        ArgumentNullException.ThrowIfNull(focuses);
        ArgumentNullException.ThrowIfNull(observations);
        var memberMap = New<PoolMember>();
        var handles = new HashSet<long>();
        foreach (var member in members)
        {
            ArgumentNullException.ThrowIfNull(member);
            if (member.Handle >= nextHandle)
            {
                throw new InvalidOperationException($"Member '{member.Id}' has handle {member.Handle}, which is not below the counter {nextHandle}.");
            }

            if (!handles.Add(member.Handle) || !memberMap.TryAdd(member.Id.Value, member))
            {
                throw new InvalidOperationException($"Member '{member.Id}' or its handle appears twice.");
            }
        }

        var lapsedMap = New<LapsedCareer>();
        foreach (var career in lapsed)
        {
            ArgumentNullException.ThrowIfNull(career);
            if (memberMap.ContainsKey(career.Id.Value) || !lapsedMap.TryAdd(career.Id.Value, career))
            {
                throw new InvalidOperationException($"Person '{career.Id}' is both lapsed and a member, or lapsed twice.");
            }
        }

        var focusMap = New<ScoutFocus>();
        foreach (var focus in focuses)
        {
            ArgumentNullException.ThrowIfNull(focus);
            if (focus.Person is PersonId target && !memberMap.ContainsKey(target.Value))
            {
                throw new InvalidOperationException($"A focus names '{target}', who is not a member.");
            }

            if (!focusMap.TryAdd(focus.Organization.Value, focus))
            {
                throw new InvalidOperationException($"Organization '{focus.Organization}' has two focuses.");
            }
        }

        var observationMap = New<ObservationRow>();
        foreach (var row in observations)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (!memberMap.ContainsKey(row.Person.Value))
            {
                throw new InvalidOperationException($"An observation names '{row.Person}', who is not a member.");
            }

            if (!observationMap.TryAdd(ObservationKey(row.Organization, row.Person), row))
            {
                throw new InvalidOperationException($"Observation of '{row.Person}' by '{row.Organization}' appears twice.");
            }
        }

        return new TalentPoolSection(nextHandle, memberMap, lapsedMap, focusMap, observationMap);
    }

    public bool Contains(PersonId person) => person.IsAssigned && _members.ContainsKey(person.Value);

    public PoolMember? Find(PersonId person) =>
        person.IsAssigned && _members.TryGetValue(person.Value, out var member) ? member : null;

    /// <summary>The member with that handle, or null when the text is not the handle of a current member.</summary>
    public PoolMember? FindByHandle(string handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.StartsWith(HandlePrefix, StringComparison.Ordinal)
            || !long.TryParse(handle.AsSpan(HandlePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || HandleOf(number) != handle)
        {
            return null;
        }

        foreach (var member in _members.Values)
        {
            if (member.Handle == number)
            {
                return member;
            }
        }

        return null;
    }

    public ScoutFocus? FocusOf(OrganizationId organization) =>
        organization.IsAssigned && _focus.TryGetValue(organization.Value, out var focus) ? focus : null;

    /// <summary>Observation points (thousandths) the organization has accumulated on a person.</summary>
    public long ObservedMilli(OrganizationId organization, PersonId person) =>
        _observations.TryGetValue(ObservationKey(organization, person), out var row) ? row.Milli : 0;

    public bool HasLapsed(PersonId person) => person.IsAssigned && _lapsed.ContainsKey(person.Value);

    /// <summary>
    /// Adds the people to the pool in one batch. Handles are given in the order of a hash of the person id, not in the order
    /// of the argument or of the id, so the number says nothing about who is real.
    /// </summary>
    public TalentPoolSection EnterAll(IEnumerable<PersonId> people, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(people);
        var batch = new List<PersonId>();
        foreach (var person in people)
        {
            if (!person.IsAssigned)
            {
                throw new ArgumentException("A person id is unassigned.", nameof(people));
            }

            if (_members.ContainsKey(person.Value) || _lapsed.ContainsKey(person.Value) || batch.Contains(person))
            {
                throw new InvalidOperationException($"Person '{person}' is already in the pool or left it.");
            }

            batch.Add(person);
        }

        batch.Sort(static (left, right) =>
        {
            var byHash = Mix(left.Value).CompareTo(Mix(right.Value));
            return byHash != 0 ? byHash : string.CompareOrdinal(left.Value, right.Value);
        });
        var members = Clone(_members);
        var next = NextHandle;
        foreach (var person in batch)
        {
            if (next == long.MaxValue)
            {
                throw new InvalidOperationException("The pool handle counter is exhausted.");
            }

            members.Add(person.Value, new PoolMember(person, next, on, null));
            next++;
        }

        return new TalentPoolSection(next, members, _lapsed, _focus, _observations);
    }

    /// <summary>
    /// Takes a person out of the pool because he signed, retired or was removed, not because his career lapsed.
    /// Unknown people are left alone. Focuses on him and observations of him end with his membership.
    /// </summary>
    public TalentPoolSection Leave(PersonId person)
    {
        if (!Contains(person))
        {
            return this;
        }

        return WithoutMember(person, _lapsed);
    }

    /// <summary>Takes a person out of the pool for good because he aged out without a contract, and records it.</summary>
    public TalentPoolSection Lapse(PersonId person, GameDate on)
    {
        if (!Contains(person))
        {
            throw new InvalidOperationException($"Person '{person}' is not in the pool.");
        }

        var lapsed = Clone(_lapsed);
        lapsed.Add(person.Value, new LapsedCareer(person, on));
        return WithoutMember(person, lapsed);
    }

    /// <summary>Records a paid junior season. A member has at most one funded season at a time.</summary>
    public TalentPoolSection Fund(PersonId person, JuniorFunding funding)
    {
        ArgumentNullException.ThrowIfNull(funding);
        var member = Find(person) ?? throw new InvalidOperationException($"Person '{person}' is not in the pool.");
        if (member.Funding is not null)
        {
            throw new InvalidOperationException($"Person '{person}' already has a funded season.");
        }

        var members = Clone(_members);
        members[person.Value] = new PoolMember(member.Id, member.Handle, member.EnteredOn, funding);
        return new TalentPoolSection(NextHandle, members, _lapsed, _focus, _observations);
    }

    /// <summary>Ends the funded season of a member once it has acted. Unknown people are left alone.</summary>
    public TalentPoolSection ClearFunding(PersonId person)
    {
        var member = Find(person);
        if (member?.Funding is null)
        {
            return this;
        }

        var members = Clone(_members);
        members[person.Value] = new PoolMember(member.Id, member.Handle, member.EnteredOn, null);
        return new TalentPoolSection(NextHandle, members, _lapsed, _focus, _observations);
    }

    /// <summary>Sets what an organization's scouts concentrate on, replacing the earlier focus.</summary>
    public TalentPoolSection SetFocus(ScoutFocus focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        if (focus.Person is PersonId target && !Contains(target))
        {
            throw new InvalidOperationException($"Person '{target}' is not in the pool.");
        }

        var focuses = Clone(_focus);
        focuses[focus.Organization.Value] = focus;
        return new TalentPoolSection(NextHandle, _members, _lapsed, focuses, _observations);
    }

    /// <summary>Adds observation points (thousandths) of an organization on a member.</summary>
    public TalentPoolSection AddObservation(OrganizationId organization, PersonId person, long milli)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milli);
        if (!organization.IsAssigned || !Contains(person))
        {
            throw new InvalidOperationException("An observation needs an organization and a current member.");
        }

        var observations = Clone(_observations);
        var key = ObservationKey(organization, person);
        var before = observations.TryGetValue(key, out var existing) ? existing.Milli : 0;
        observations[key] = new ObservationRow(organization, person, checked(before + milli));
        return new TalentPoolSection(NextHandle, _members, _lapsed, _focus, observations);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", NextHandle);
        writer.Count("members", _members.Count);
        foreach (var member in _members.Values)
        {
            writer.Begin("member");
            writer.Field(member.Id.Value);
            writer.Space();
            writer.Raw(PoolText.Invariant(member.Handle));
            writer.Space();
            writer.Field(member.EnteredOn.ToString());
            writer.End();
            writer.Flag("funding", member.Funding is not null);
            if (member.Funding is { } funding)
            {
                writer.TextLine("funder", funding.Funder.Value);
                writer.Line("programme " + funding.Programme.ToString());
                writer.Number("season", funding.Season);
            }
        }

        writer.Count("lapsed", _lapsed.Count);
        foreach (var career in _lapsed.Values)
        {
            writer.Begin("lapse");
            writer.Field(career.Id.Value);
            writer.Space();
            writer.Field(career.On.ToString());
            writer.End();
        }

        writer.Count("focus", _focus.Count);
        foreach (var focus in _focus.Values)
        {
            writer.Begin("focus");
            writer.Field(focus.Organization.Value);
            writer.Space();
            writer.Raw(focus.Kind.ToString());
            writer.Space();
            writer.Field(focus.Person?.Value ?? "-");
            writer.End();
        }

        writer.Count("observations", _observations.Count);
        foreach (var row in _observations.Values)
        {
            writer.Begin("observation");
            writer.Field(row.Organization.Value);
            writer.Space();
            writer.Field(row.Person.Value);
            writer.Space();
            writer.Raw(PoolText.Invariant(row.Milli));
            writer.End();
        }
    }

    private TalentPoolSection WithoutMember(PersonId person, SortedDictionary<string, LapsedCareer> lapsed)
    {
        var members = Clone(_members);
        members.Remove(person.Value);
        var focuses = new SortedDictionary<string, ScoutFocus>(StringComparer.Ordinal);
        foreach (var pair in _focus)
        {
            if (pair.Value.Person != person)
            {
                focuses.Add(pair.Key, pair.Value);
            }
        }

        var observations = New<ObservationRow>();
        foreach (var pair in _observations)
        {
            if (pair.Value.Person != person)
            {
                observations.Add(pair.Key, pair.Value);
            }
        }

        return new TalentPoolSection(NextHandle, members, lapsed, focuses, observations);
    }

    private static string ObservationKey(OrganizationId organization, PersonId person) =>
        organization.Value + "\u001f" + person.Value;

    private static ulong Mix(string text) => Fnv1a64.Hash(Encoding.UTF8.GetBytes(text));

    private static SortedDictionary<string, T> New<T>() => new(StringComparer.Ordinal);

    private static SortedDictionary<string, T> Clone<T>(SortedDictionary<string, T> source) => new(source, StringComparer.Ordinal);
}
