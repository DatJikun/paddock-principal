using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>One attribute of a person as the organization believes it: a band, never a number.</summary>
public sealed record KnownAttributeView(string Key, int Low, int High);

/// <summary>One line of a negotiation's history. <see cref="Reasons"/> are the reasons the person stated.</summary>
public sealed record NegotiationRoundView(int Number, RoundKind Kind, DateOnly On, OfferTerms? Terms, IReadOnlyList<TranslationMessage> Reasons);

/// <summary>
/// What a negotiation is about, in a shape every reader can take: <see cref="StaffRole"/> is null for a driver seat, because
/// the domain subject refuses to name a staff role it does not have.
/// </summary>
public sealed record NegotiationSubjectView(string Key, NegotiationSubjectKind Kind, StaffRole? StaffRole);

/// <summary>
/// A negotiation as its manager reads it. The person is shown by name and by the bands the proposing organization believes
/// (INV-003); the personality, the weights and the true attributes appear nowhere. <see cref="Interest"/> is a word, not a
/// number. <see cref="Reasons"/> is never empty after a counter or a refusal.
/// </summary>
public sealed record NegotiationView(
    string Id,
    string ManagerId,
    OrganizationId Proposer,
    PersonId Person,
    string PersonName,
    string Nationality,
    NegotiationSubjectView Subject,
    NegotiationStatus Status,
    TranslationMessage StatusText,
    int RoundsUsed,
    int MaxRounds,
    TranslationMessage Interest,
    DateOnly Opened,
    DateOnly Deadline,
    DateOnly? RespondOn,
    OfferTerms? Offer,
    OfferTerms? Counter,
    IReadOnlyList<TranslationMessage> Reasons,
    IReadOnlyList<NegotiationRoundView> History,
    IReadOnlyList<KnownAttributeView> KnownAttributes,
    ContractId? SignedContract);

/// <summary>The negotiations one viewer may see.</summary>
public sealed record NegotiationsView(AccessContext Viewer, IReadOnlyList<NegotiationView> Items);

/// <summary>
/// The read side of negotiations (INV-003, INV-005). A manager or an AI manager sees only their own negotiations; the developer
/// sees all of them. It changes nothing and draws no RNG.
/// </summary>
public sealed class NegotiationQuery
{
    private readonly ContractBook _book;

    public NegotiationQuery(ContractBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public NegotiationsView View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        IEnumerable<Negotiation> visible = access.Kind == AccessKind.Developer
            ? _book.Section.Negotiations
            : _book.Section.OfManager((access.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.")).Value);
        return new NegotiationsView(access, visible.Select(Describe).ToArray());
    }

    private NegotiationView Describe(Negotiation negotiation)
    {
        var person = _book.World.GetPerson(negotiation.Counterparty);
        var attributes = _book.World.KnowledgeOf(negotiation.Proposer, negotiation.Counterparty) is PersonKnowledgeView belief
            ? belief.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()
            : [];
        return new NegotiationView(
            negotiation.Id,
            negotiation.ManagerId,
            negotiation.Proposer,
            negotiation.Counterparty,
            person.Name,
            person.Nationality,
            new NegotiationSubjectView(
                negotiation.Subject.Key,
                negotiation.Subject.Kind,
                negotiation.Subject.Kind == NegotiationSubjectKind.StaffRole ? negotiation.Subject.StaffRole : null),
            negotiation.Status,
            TranslationMessage.Of(ContractKeys.StatusKey(negotiation.Status)),
            negotiation.RoundsUsed,
            negotiation.MaxRounds,
            TranslationMessage.Of(ContractKeys.InterestKey(negotiation.Interest)),
            InboxBook.ToDateOnly(negotiation.Opened),
            InboxBook.ToDateOnly(negotiation.Deadline),
            negotiation.RespondOn is GameDate respondOn ? InboxBook.ToDateOnly(respondOn) : null,
            negotiation.CurrentOffer,
            negotiation.Counter,
            Messages(negotiation.Reasons),
            negotiation.History
                .Select(round => new NegotiationRoundView(round.Number, round.Kind, InboxBook.ToDateOnly(round.On), round.Terms, Messages(round.Reasons)))
                .ToArray(),
            attributes,
            negotiation.SignedContract);
    }

    private static TranslationMessage[] Messages(IReadOnlyList<string> keys) =>
        keys.Select(key => TranslationMessage.Of(key)).ToArray();
}

/// <summary>One person on the public free-agent list. The attributes are bands (<see cref="KnownAttributeView"/>).</summary>
public sealed record FreeAgentView(
    PersonId Person,
    string Name,
    string Nationality,
    bool IsDriver,
    IReadOnlyList<StaffRole> StaffRoles,
    DateOnly? FreeSince,
    IReadOnlyList<KnownAttributeView> KnownAttributes);

/// <summary>
/// The public list of people with no contract (DESIGN section 10, free agent at expiry). Attributes come as bands: what the asking
/// organization believes about the person, and nothing for a person it has no belief about (scouting narrows the bands, T40).
/// A manager must run the organization they ask as; the developer sees the simulation truth as a band of one value.
/// A pure query: no state change, no RNG (INV-005).
/// </summary>
public sealed class FreeAgentQuery
{
    private readonly ContractBook _book;

    public FreeAgentQuery(ContractBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    /// <summary>
    /// The free agents on <paramref name="today"/>, in order of person id. Pass <paramref name="subject"/> to list only people who can
    /// fill that role. For a manager the list is empty unless they run <paramref name="observer"/>.
    /// </summary>
    public IReadOnlyList<FreeAgentView> List(AccessContext access, OrganizationId observer, GameDate today, NegotiationSubject? subject = null)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Kind != AccessKind.Developer
            && (access.Manager is not { } manager
                || !_book.Environment.Control.Controls(new Managers.ManagerId(manager.Value), observer)))
        {
            return [];
        }

        var busy = new HashSet<string>(StringComparer.Ordinal);
        var lastEnd = new Dictionary<string, GameDate>(StringComparer.Ordinal);
        foreach (var contract in _book.World.Contracts)
        {
            if (!contract.Exclusive)
            {
                continue;
            }

            if (contract.End >= today)
            {
                busy.Add(contract.PersonId.Value);
            }
            else if (!lastEnd.TryGetValue(contract.PersonId.Value, out var known) || contract.End > known)
            {
                lastEnd[contract.PersonId.Value] = contract.End;
            }
        }

        var result = new List<FreeAgentView>();
        foreach (var person in _book.World.Persons)
        {
            if (busy.Contains(person.Id.Value)
                || person.IsRetired
                || (subject is NegotiationSubject wanted && !wanted.IsHeldBy(person.Roles)))
            {
                continue;
            }

            var roles = person.Roles.Where(role => role.IsStaff).Select(role => role.StaffRole).ToArray();
            var isDriver = person.Roles.Any(role => role.IsDriver);
            IReadOnlyList<KnownAttributeView> attributes = access.Kind == AccessKind.Developer
                ? person.Truth.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Value, attribute.Value)).ToArray()
                : _book.World.KnowledgeOf(observer, person.Id) is PersonKnowledgeView belief
                    ? belief.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()
                    : [];
            result.Add(new FreeAgentView(
                person.Id,
                person.Name,
                person.Nationality,
                isDriver,
                roles,
                lastEnd.TryGetValue(person.Id.Value, out var since) ? InboxBook.ToDateOnly(since.AddDays(1)) : null,
                attributes));
        }

        return result.OrderBy(view => view.Person.Value, StringComparer.Ordinal).ToArray();
    }
}
