using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// The contracts and negotiations of the world, as the section named <see cref="SectionName"/>. It extends the T15
/// <see cref="Contract"/> (which stays in <see cref="WorldState"/>) with <see cref="ContractTerms"/> by contract id and holds every
/// <see cref="Negotiation"/>. Numbers of negotiations come from this section's own counter, which only moves forward, so an id
/// is never reused (INV-009); the world-level allocator is untouched, so a world with no negotiations hashes as before.
/// Immutable: every change returns a new section. Closed negotiations stay as records (nothing prunes them yet).
/// This is truth: who may read which negotiation is decided one layer up.
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash. Lines are written through
/// <see cref="CanonicalWriter"/>; a terms line is <c>label salary points win title years seat option exit</c> with <c>-</c> for none:
/// <code>
/// next &lt;nextNegotiation&gt;
/// terms &lt;count&gt;
/// term &lt;len&gt;:&lt;contractId&gt; &lt;points&gt; &lt;win&gt; &lt;title&gt; &lt;Team|Person|-&gt; &lt;exitPosition|-&gt;
/// negotiations &lt;count&gt;
/// negotiation &lt;len&gt;:&lt;id&gt;
/// manager, proposer, person, subject, renewal, opened, deadline   (one text line each)
/// rounds &lt;max&gt; &lt;used&gt;
/// interest &lt;thousandths&gt;
/// status &lt;Status&gt;
/// respond &lt;len&gt;:&lt;date or -&gt;
/// utility &lt;micro&gt;
/// offer ... / counter ...                                         (a terms line, or the label and -)
/// reasons &lt;count&gt; then reason &lt;len&gt;:&lt;key&gt;
/// history &lt;count&gt; then round &lt;n&gt; &lt;Kind&gt; &lt;len&gt;:&lt;date&gt;, a terms line, reasons
/// closed &lt;len&gt;:&lt;date or -&gt;
/// signed &lt;len&gt;:&lt;contractId or -&gt;
/// prompts &lt;count&gt; then prompt &lt;len&gt;:&lt;contractId&gt;
/// </code>
/// </para>
/// </summary>
public sealed class ContractsSection : IWorldSection
{
    public const string SectionName = "contracts";

    private readonly SortedDictionary<string, ContractTerms> _terms;
    private readonly SortedDictionary<long, Negotiation> _negotiations;
    private readonly SortedSet<string> _prompted;

    private ContractsSection(
        long nextNegotiation,
        SortedDictionary<string, ContractTerms> terms,
        SortedDictionary<long, Negotiation> negotiations,
        SortedSet<string> prompted)
    {
        NextNegotiation = nextNegotiation;
        _terms = terms;
        _negotiations = negotiations;
        _prompted = prompted;
    }

    public static ContractsSection Empty { get; } = new(
        1,
        new SortedDictionary<string, ContractTerms>(StringComparer.Ordinal),
        [],
        new SortedSet<string>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    /// <summary>The number the next negotiation gets. Starts at 1.</summary>
    public long NextNegotiation { get; }

    /// <summary>True when the section holds nothing and never issued an id. Such a section is left out of the world.</summary>
    public bool IsEmpty => NextNegotiation == 1 && _terms.Count == 0 && _prompted.Count == 0;

    public IReadOnlyList<ContractTerms> Terms => _terms.Values.ToArray();

    /// <summary>All negotiations, alive and closed, in order of their number.</summary>
    public IReadOnlyList<Negotiation> Negotiations => _negotiations.Values.ToArray();

    /// <summary>The contracts a renewal prompt has already been sent for, in ordinal order.</summary>
    public IReadOnlyList<ContractId> PromptedRenewals => _prompted.Select(id => ContractIdOf(id)).ToArray();

    /// <summary>
    /// Rebuilds a section from stored rows. Every negotiation number must be below <paramref name="nextNegotiation"/>.
    /// </summary>
    public static ContractsSection Restore(
        long nextNegotiation,
        IEnumerable<ContractTerms> terms,
        IEnumerable<Negotiation> negotiations,
        IEnumerable<ContractId> promptedRenewals)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextNegotiation, 1);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(negotiations);
        ArgumentNullException.ThrowIfNull(promptedRenewals);
        var termMap = new SortedDictionary<string, ContractTerms>(StringComparer.Ordinal);
        foreach (var item in terms)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!termMap.TryAdd(item.ContractId.Value, item))
            {
                throw new InvalidOperationException($"Contract '{item.ContractId}' has terms twice.");
            }
        }

        var map = new SortedDictionary<long, Negotiation>();
        foreach (var negotiation in negotiations)
        {
            ArgumentNullException.ThrowIfNull(negotiation);
            if (negotiation.Number >= nextNegotiation)
            {
                throw new InvalidOperationException($"Negotiation '{negotiation.Id}' is not below the counter {nextNegotiation}.");
            }

            if (!map.TryAdd(negotiation.Number, negotiation))
            {
                throw new InvalidOperationException($"Negotiation '{negotiation.Id}' appears twice.");
            }
        }

        var prompts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var id in promptedRenewals)
        {
            if (!id.IsAssigned || !prompts.Add(id.Value))
            {
                throw new InvalidOperationException("A renewal prompt is missing its contract or appears twice.");
            }
        }

        return new ContractsSection(nextNegotiation, termMap, map, prompts);
    }

    public ContractTerms? TermsOf(ContractId contract) =>
        contract.IsAssigned && _terms.TryGetValue(contract.Value, out var terms) ? terms : null;

    public Negotiation? Find(string negotiationId)
    {
        ArgumentNullException.ThrowIfNull(negotiationId);
        if (!negotiationId.StartsWith(Negotiation.IdPrefix, StringComparison.Ordinal)
            || !long.TryParse(negotiationId.AsSpan(Negotiation.IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || Negotiation.IdOf(number) != negotiationId)
        {
            return null;
        }

        return _negotiations.GetValueOrDefault(number);
    }

    /// <summary>The negotiations that can still end in a contract, in order of their number.</summary>
    public IReadOnlyList<Negotiation> Active() => _negotiations.Values.Where(n => n.IsActive).ToArray();

    /// <summary>The live negotiations run by one organization.</summary>
    public IReadOnlyList<Negotiation> ActiveOf(OrganizationId proposer) =>
        _negotiations.Values.Where(n => n.IsActive && n.Proposer == proposer).ToArray();

    /// <summary>The live negotiations about one person, whoever runs them.</summary>
    public IReadOnlyList<Negotiation> ActiveFor(PersonId person) =>
        _negotiations.Values.Where(n => n.IsActive && n.Counterparty == person).ToArray();

    /// <summary>The negotiations of one manager, alive and closed.</summary>
    public IReadOnlyList<Negotiation> OfManager(string managerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        return _negotiations.Values.Where(n => n.ManagerId == managerId).ToArray();
    }

    public bool WasPrompted(ContractId contract) => contract.IsAssigned && _prompted.Contains(contract.Value);

    /// <summary>Opens a negotiation and gives it the next number.</summary>
    public (ContractsSection Section, Negotiation Negotiation) Open(
        string managerId,
        OrganizationId proposer,
        PersonId counterparty,
        NegotiationSubject subject,
        ContractId? renewalOf,
        GameDate opened,
        GameDate deadline,
        int maxRounds)
    {
        if (NextNegotiation == long.MaxValue)
        {
            throw new InvalidOperationException("The negotiation id counter is exhausted.");
        }

        var negotiation = Negotiation.Start(NextNegotiation, managerId, proposer, counterparty, subject, renewalOf, opened, deadline, maxRounds);
        var map = new SortedDictionary<long, Negotiation>(_negotiations) { [negotiation.Number] = negotiation };
        return (new ContractsSection(NextNegotiation + 1, _terms, map, _prompted), negotiation);
    }

    /// <summary>Puts the changed negotiation back. The negotiation must already exist here.</summary>
    public ContractsSection Replace(Negotiation negotiation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        if (!_negotiations.ContainsKey(negotiation.Number))
        {
            throw new InvalidOperationException($"Unknown negotiation '{negotiation.Id}'.");
        }

        var map = new SortedDictionary<long, Negotiation>(_negotiations) { [negotiation.Number] = negotiation };
        return new ContractsSection(NextNegotiation, _terms, map, _prompted);
    }

    /// <summary>Records the terms of a contract.</summary>
    public ContractsSection WithTerms(ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var map = new SortedDictionary<string, ContractTerms>(_terms, StringComparer.Ordinal) { [terms.ContractId.Value] = terms };
        return new ContractsSection(NextNegotiation, map, _negotiations, _prompted);
    }

    /// <summary>
    /// Drops the terms and renewal prompts of contracts that no longer exist (a person who retires loses their contracts, which
    /// the world then removes). Negotiations are kept as records. Returns this section when nothing is dropped.
    /// </summary>
    public ContractsSection PruneTo(Func<ContractId, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var terms = new SortedDictionary<string, ContractTerms>(StringComparer.Ordinal);
        foreach (var pair in _terms)
        {
            if (exists(pair.Value.ContractId))
            {
                terms.Add(pair.Key, pair.Value);
            }
        }

        var prompts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var id in _prompted)
        {
            if (exists(ContractIdOf(id)))
            {
                prompts.Add(id);
            }
        }

        return terms.Count == _terms.Count && prompts.Count == _prompted.Count
            ? this
            : new ContractsSection(NextNegotiation, terms, _negotiations, prompts);
    }

    /// <summary>Notes that the renewal prompt for a contract has been sent.</summary>
    public ContractsSection WithRenewalPrompt(ContractId contract)
    {
        if (!contract.IsAssigned)
        {
            throw new ArgumentException("Contract id is unassigned.", nameof(contract));
        }

        var prompts = new SortedSet<string>(_prompted, StringComparer.Ordinal) { contract.Value };
        return new ContractsSection(NextNegotiation, _terms, _negotiations, prompts);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", NextNegotiation);
        writer.Count("terms", _terms.Count);
        foreach (var terms in _terms.Values)
        {
            writer.Begin("term");
            writer.Field(terms.ContractId.Value);
            writer.Raw(
                " " + terms.PointsBonus.ToString(CultureInfo.InvariantCulture)
                + " " + terms.WinBonus.ToString(CultureInfo.InvariantCulture)
                + " " + terms.TitleBonus.ToString(CultureInfo.InvariantCulture)
                + " " + (terms.OptionHolder is OptionHolder holder ? holder.ToString() : "-")
                + " " + (terms.Exit is ExitClause exit ? exit.PositionWorseThan.ToString(CultureInfo.InvariantCulture) : "-"));
            writer.End();
        }

        writer.Count("negotiations", _negotiations.Count);
        foreach (var negotiation in _negotiations.Values)
        {
            writer.TextLine("negotiation", negotiation.Id);
            writer.TextLine("manager", negotiation.ManagerId);
            writer.TextLine("proposer", negotiation.Proposer.Value);
            writer.TextLine("person", negotiation.Counterparty.Value);
            writer.TextLine("subject", negotiation.Subject.Key);
            writer.TextLine("renewal", negotiation.RenewalOf?.Value ?? "-");
            writer.TextLine("opened", negotiation.Opened.ToString());
            writer.TextLine("deadline", negotiation.Deadline.ToString());
            writer.Line(
                "rounds " + negotiation.MaxRounds.ToString(CultureInfo.InvariantCulture)
                + " " + negotiation.RoundsUsed.ToString(CultureInfo.InvariantCulture));
            writer.Number("interest", negotiation.Interest);
            writer.Line("status " + negotiation.Status);
            writer.TextLine("respond", negotiation.RespondOn?.ToString() ?? "-");
            writer.Number("utility", negotiation.ConsideredUtilityMicro);
            WriteTerms(writer, "offer", negotiation.CurrentOffer);
            WriteTerms(writer, "counter", negotiation.Counter);
            WriteReasons(writer, negotiation.Reasons);
            writer.Count("history", negotiation.History.Count);
            foreach (var round in negotiation.History)
            {
                writer.Begin("round");
                writer.Raw(round.Number.ToString(CultureInfo.InvariantCulture) + " " + round.Kind + " ");
                writer.Field(round.On.ToString());
                writer.End();
                WriteTerms(writer, "terms", round.Terms);
                WriteReasons(writer, round.Reasons);
            }

            writer.TextLine("closed", negotiation.ClosedOn?.ToString() ?? "-");
            writer.TextLine("signed", negotiation.SignedContract?.Value ?? "-");
        }

        writer.Count("prompts", _prompted.Count);
        foreach (var id in _prompted)
        {
            writer.TextLine("prompt", id);
        }
    }

    private static void WriteTerms(CanonicalWriter writer, string label, OfferTerms? terms)
    {
        if (terms is null)
        {
            writer.Line(label + " -");
            return;
        }

        terms.WriteCanonical(writer, label);
    }

    private static void WriteReasons(CanonicalWriter writer, IReadOnlyList<string> reasons)
    {
        writer.Count("reasons", reasons.Count);
        foreach (var reason in reasons)
        {
            writer.TextLine("reason", reason);
        }
    }

    private static ContractId ContractIdOf(string canonical)
    {
        if (!canonical.StartsWith(IdText.ContractPrefix, StringComparison.Ordinal)
            || !long.TryParse(canonical.AsSpan(IdText.ContractPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
        {
            throw new InvalidOperationException($"'{canonical}' is not a contract id.");
        }

        return ContractId.Generated(sequence);
    }
}
