using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Sponsors;

/// <summary>
/// The <c>sponsors</c> section (T38): talks, deals, renewal offers, per-(sponsor, organization) trust, and the sponsors a rival
/// has taken off the market. Immutable. One counter numbers talks, deals and offers (ids <c>spt:{n}</c>, <c>spd:{n}</c>,
/// <c>spo:{n}</c>), so an id is never reused (INV-009). Money is not held here: instalments and bonuses are posted to the
/// finance ledger by the day handler and the commands.
/// <para>
/// Canonical text (schema 1), after the section header. A missing value is <c>-</c>.
/// <code>
/// next &lt;n&gt;
/// talks &lt;count&gt;
/// talk &lt;number&gt; &lt;len&gt;:&lt;organization&gt; &lt;len&gt;:&lt;sponsor&gt; &lt;slot&gt; &lt;kind&gt; &lt;len&gt;:&lt;manager&gt; &lt;opened&gt; &lt;status&gt; &lt;closed|-&gt; &lt;rival Undecided|Absent|Present&gt; &lt;capMilli&gt; &lt;fullCents&gt;
/// deals &lt;count&gt;
/// deal &lt;number&gt; &lt;len&gt;:&lt;organization&gt; &lt;len&gt;:&lt;sponsor&gt; &lt;slot&gt; &lt;kind&gt; &lt;start&gt; &lt;end&gt; &lt;annualCents&gt; &lt;instalments&gt; &lt;len&gt;:&lt;objective|-&gt; &lt;outcome&gt; &lt;bonusCents&gt; &lt;status&gt; &lt;ended|-&gt;
/// offers &lt;count&gt;
/// offer &lt;number&gt; &lt;deal&gt; &lt;len&gt;:&lt;organization&gt; &lt;len&gt;:&lt;sponsor&gt; &lt;slot&gt; &lt;kind&gt; &lt;annualCents&gt; &lt;opened&gt; &lt;validUntil&gt; &lt;status&gt; &lt;closed|-&gt;
/// trusts &lt;count&gt;
/// trust &lt;len&gt;:&lt;sponsor&gt; &lt;len&gt;:&lt;organization&gt; &lt;value&gt;
/// taken &lt;count&gt;
/// take &lt;len&gt;:&lt;sponsor&gt; &lt;until&gt;
/// </code>
/// </para>
/// </summary>
public sealed class SponsorsSection : IWorldSection
{
    public const string SectionName = "sponsors";

    private readonly SortedDictionary<long, SponsorTalk> _talks;
    private readonly SortedDictionary<long, SponsorDeal> _deals;
    private readonly SortedDictionary<long, SponsorOffer> _offers;
    private readonly SortedDictionary<string, int> _trust;
    private readonly SortedDictionary<string, GameDate> _taken;

    private SponsorsSection(
        long next,
        SortedDictionary<long, SponsorTalk> talks,
        SortedDictionary<long, SponsorDeal> deals,
        SortedDictionary<long, SponsorOffer> offers,
        SortedDictionary<string, int> trust,
        SortedDictionary<string, GameDate> taken)
    {
        Next = next;
        _talks = talks;
        _deals = deals;
        _offers = offers;
        _trust = trust;
        _taken = taken;
    }

    public static SponsorsSection Empty { get; } = new(
        1,
        [],
        [],
        [],
        new SortedDictionary<string, int>(StringComparer.Ordinal),
        new SortedDictionary<string, GameDate>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long Next { get; }

    public IReadOnlyList<SponsorTalk> Talks => _talks.Values.ToArray();

    public IReadOnlyList<SponsorDeal> Deals => _deals.Values.ToArray();

    public IReadOnlyList<SponsorOffer> Offers => _offers.Values.ToArray();

    public static string TalkIdOf(long number) => "spt:" + SponsorText.Invariant(number);

    public static string DealIdOf(long number) => "spd:" + SponsorText.Invariant(number);

    public static string OfferIdOf(long number) => "spo:" + SponsorText.Invariant(number);

    public SponsorTalk? FindTalk(string id) => TryNumber(id, "spt:", out var number) ? _talks.GetValueOrDefault(number) : null;

    public SponsorDeal? FindDeal(string id) => TryNumber(id, "spd:", out var number) ? _deals.GetValueOrDefault(number) : null;

    public SponsorOffer? FindOffer(string id) => TryNumber(id, "spo:", out var number) ? _offers.GetValueOrDefault(number) : null;

    public SponsorDeal? DealByObjective(string objectiveId) =>
        _deals.Values.FirstOrDefault(deal => deal.ObjectiveId == objectiveId);

    public IReadOnlyList<SponsorTalk> OpenTalksOf(OrganizationId organization) =>
        _talks.Values.Where(talk => talk.IsOpen && talk.Organization == organization).ToArray();

    public IReadOnlyList<SponsorDeal> ActiveDealsOf(OrganizationId organization) =>
        _deals.Values.Where(deal => deal.IsActive && deal.Organization == organization).ToArray();

    public IReadOnlyList<SponsorOffer> OpenOffersOf(OrganizationId organization) =>
        _offers.Values.Where(offer => offer.IsOpen && offer.Organization == organization).ToArray();

    /// <summary>True when the slot of the organization has an active deal or an open talk, so nothing else may use it.</summary>
    public bool SlotBusy(OrganizationId organization, int slot) =>
        _deals.Values.Any(deal => deal.IsActive && deal.Organization == organization && deal.Slot == slot)
        || _talks.Values.Any(talk => talk.IsOpen && talk.Organization == organization && talk.Slot == slot);

    /// <summary>True when the sponsor is under an active deal with any organization (a sponsor backs one team at a time).</summary>
    public bool SponsorInDeal(string sponsorId) =>
        _deals.Values.Any(deal => deal.IsActive && deal.SponsorId == sponsorId);

    public bool IsTaken(string sponsorId, GameDate today) =>
        _taken.TryGetValue(sponsorId, out var until) && today <= until;

    public int TrustOf(string sponsorId, OrganizationId organization) =>
        _trust.TryGetValue(TrustKey(sponsorId, organization), out var value) ? value : SponsorEstimates.StartTrust;

    public SponsorsSection WithTrust(string sponsorId, OrganizationId organization, int trust)
    {
        var copy = new SortedDictionary<string, int>(_trust, StringComparer.Ordinal)
        {
            [TrustKey(sponsorId, organization)] = SponsorPricing.ClampTrust(trust),
        };
        return new SponsorsSection(Next, _talks, _deals, _offers, copy, _taken);
    }

    public SponsorsSection Take(string sponsorId, GameDate until)
    {
        var copy = new SortedDictionary<string, GameDate>(_taken, StringComparer.Ordinal) { [sponsorId] = until };
        return new SponsorsSection(Next, _talks, _deals, _offers, _trust, copy);
    }

    public (SponsorsSection Section, SponsorTalk Talk) AddTalk(SponsorTalk talk)
    {
        ArgumentNullException.ThrowIfNull(talk);
        var numbered = talk with { Number = Counter() };
        var copy = new SortedDictionary<long, SponsorTalk>(_talks) { [numbered.Number] = numbered };
        return (new SponsorsSection(Next + 1, copy, _deals, _offers, _trust, _taken), numbered);
    }

    public (SponsorsSection Section, SponsorDeal Deal) AddDeal(SponsorDeal deal)
    {
        ArgumentNullException.ThrowIfNull(deal);
        var numbered = deal with { Number = Counter() };
        var copy = new SortedDictionary<long, SponsorDeal>(_deals) { [numbered.Number] = numbered };
        return (new SponsorsSection(Next + 1, _talks, copy, _offers, _trust, _taken), numbered);
    }

    public (SponsorsSection Section, SponsorOffer Offer) AddOffer(SponsorOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var numbered = offer with { Number = Counter() };
        var copy = new SortedDictionary<long, SponsorOffer>(_offers) { [numbered.Number] = numbered };
        return (new SponsorsSection(Next + 1, _talks, _deals, copy, _trust, _taken), numbered);
    }

    public SponsorsSection Replace(SponsorTalk talk)
    {
        Require(_talks.ContainsKey(talk.Number), talk.Id);
        var copy = new SortedDictionary<long, SponsorTalk>(_talks) { [talk.Number] = talk };
        return new SponsorsSection(Next, copy, _deals, _offers, _trust, _taken);
    }

    public SponsorsSection Replace(SponsorDeal deal)
    {
        Require(_deals.ContainsKey(deal.Number), deal.Id);
        var copy = new SortedDictionary<long, SponsorDeal>(_deals) { [deal.Number] = deal };
        return new SponsorsSection(Next, _talks, copy, _offers, _trust, _taken);
    }

    public SponsorsSection Replace(SponsorOffer offer)
    {
        Require(_offers.ContainsKey(offer.Number), offer.Id);
        var copy = new SortedDictionary<long, SponsorOffer>(_offers) { [offer.Number] = offer };
        return new SponsorsSection(Next, _talks, _deals, copy, _trust, _taken);
    }

    /// <summary>Rebuilds a section from stored rows. Numbers are unique across the three kinds and below <paramref name="next"/>.</summary>
    public static SponsorsSection Restore(
        long next,
        IEnumerable<SponsorTalk> talks,
        IEnumerable<SponsorDeal> deals,
        IEnumerable<SponsorOffer> offers,
        IEnumerable<(string Sponsor, string Organization, int Trust)> trust,
        IEnumerable<(string Sponsor, GameDate Until)> taken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(next, 1);
        var numbers = new HashSet<long>();
        var talkMap = new SortedDictionary<long, SponsorTalk>();
        var dealMap = new SortedDictionary<long, SponsorDeal>();
        var offerMap = new SortedDictionary<long, SponsorOffer>();
        foreach (var talk in talks)
        {
            Claim(numbers, talk.Number, next);
            talkMap.Add(talk.Number, talk);
        }

        foreach (var deal in deals)
        {
            Claim(numbers, deal.Number, next);
            dealMap.Add(deal.Number, deal);
        }

        foreach (var offer in offers)
        {
            Claim(numbers, offer.Number, next);
            offerMap.Add(offer.Number, offer);
        }

        if (numbers.Count != next - 1)
        {
            throw new InvalidOperationException("The sponsor counter does not match the number of talks, deals and offers.");
        }

        var trustMap = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (sponsor, organization, value) in trust)
        {
            trustMap[TrustKey(sponsor, ParseOrganization(organization))] = SponsorPricing.ClampTrust(value);
        }

        var takenMap = new SortedDictionary<string, GameDate>(StringComparer.Ordinal);
        foreach (var (sponsor, until) in taken)
        {
            takenMap[sponsor] = until;
        }

        return new SponsorsSection(next, talkMap, dealMap, offerMap, trustMap, takenMap);
    }

    public IReadOnlyList<(string Sponsor, string Organization, int Trust)> TrustRows() =>
        _trust.Select(pair =>
        {
            var split = pair.Key.IndexOf('|', StringComparison.Ordinal);
            return (pair.Key[..split], pair.Key[(split + 1)..], pair.Value);
        }).ToArray();

    public IReadOnlyList<(string Sponsor, GameDate Until)> TakenRows() =>
        _taken.Select(pair => (pair.Key, pair.Value)).ToArray();

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", Next);
        writer.Count("talks", _talks.Count);
        foreach (var talk in _talks.Values)
        {
            writer.Begin("talk");
            writer.Raw(SponsorText.Invariant(talk.Number));
            writer.Space();
            writer.Field(talk.Organization.Value);
            writer.Space();
            writer.Field(talk.SponsorId);
            writer.Raw(" " + SponsorText.Invariant(talk.Slot) + " " + SlotKinds.KeyOf(talk.Kind) + " ");
            writer.Field(talk.ManagerId);
            writer.Raw(" " + talk.Opened + " " + talk.Status + " " + (talk.ClosedOn?.ToString() ?? "-")
                + " " + talk.Rival + " " + SponsorText.Invariant(talk.CapMilli)
                + " " + SponsorText.Invariant(talk.FullAnnualCents));
            writer.End();
        }

        writer.Count("deals", _deals.Count);
        foreach (var deal in _deals.Values)
        {
            writer.Begin("deal");
            writer.Raw(SponsorText.Invariant(deal.Number));
            writer.Space();
            writer.Field(deal.Organization.Value);
            writer.Space();
            writer.Field(deal.SponsorId);
            writer.Raw(" " + SponsorText.Invariant(deal.Slot) + " " + SlotKinds.KeyOf(deal.Kind) + " " + deal.Start + " " + deal.End
                + " " + SponsorText.Invariant(deal.AnnualCents) + " " + SponsorText.Invariant(deal.InstalmentsPaid) + " ");
            writer.Field(deal.ObjectiveId ?? "-");
            writer.Raw(" " + deal.Outcome + " " + SponsorText.Invariant(deal.BonusCents) + " " + deal.Status + " " + (deal.EndedOn?.ToString() ?? "-"));
            writer.End();
        }

        writer.Count("offers", _offers.Count);
        foreach (var offer in _offers.Values)
        {
            writer.Begin("offer");
            writer.Raw(SponsorText.Invariant(offer.Number) + " " + SponsorText.Invariant(offer.DealNumber) + " ");
            writer.Field(offer.Organization.Value);
            writer.Space();
            writer.Field(offer.SponsorId);
            writer.Raw(" " + SponsorText.Invariant(offer.Slot) + " " + SlotKinds.KeyOf(offer.Kind) + " " + SponsorText.Invariant(offer.AnnualCents)
                + " " + offer.Opened + " " + offer.ValidUntil + " " + offer.Status + " " + (offer.ClosedOn?.ToString() ?? "-"));
            writer.End();
        }

        writer.Count("trusts", _trust.Count);
        foreach (var (sponsor, organization, value) in TrustRows())
        {
            writer.Begin("trust");
            writer.Field(sponsor);
            writer.Space();
            writer.Field(organization);
            writer.Raw(" " + SponsorText.Invariant(value));
            writer.End();
        }

        writer.Count("taken", _taken.Count);
        foreach (var (sponsor, until) in _taken)
        {
            writer.Begin("take");
            writer.Field(sponsor);
            writer.Raw(" " + until);
            writer.End();
        }
    }

    private long Counter()
    {
        if (Next == long.MaxValue)
        {
            throw new InvalidOperationException("The sponsor id counter is exhausted.");
        }

        return Next;
    }

    private static void Require(bool condition, string id)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Unknown sponsor record '" + id + "'.");
        }
    }

    private static void Claim(HashSet<long> numbers, long number, long next)
    {
        if (number < 1 || number >= next || !numbers.Add(number))
        {
            throw new InvalidOperationException("Sponsor record number " + SponsorText.Invariant(number) + " is duplicated or past the counter.");
        }
    }

    private static string TrustKey(string sponsorId, OrganizationId organization) => sponsorId + "|" + organization.Value;

    private static bool TryNumber(string id, string prefix, out long number)
    {
        number = 0;
        return id is not null
            && id.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number >= 1;
    }

    /// <summary>Reads an organization id written by <see cref="OrganizationId.Value"/>: <c>org:{n}</c> is generated, anything else real.</summary>
    public static OrganizationId ParseOrganization(string value)
    {
        const string prefix = "org:";
        if (value.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            && sequence >= 1)
        {
            return OrganizationId.Generated(sequence);
        }

        return OrganizationId.Real(value);
    }
}
