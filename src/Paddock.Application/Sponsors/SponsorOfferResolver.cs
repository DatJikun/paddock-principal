using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Domain.Sponsors;

namespace Paddock.Application.Sponsors;

/// <summary>Stable codes of the renewal-offer decision. Codes, not text: the text is in the translation keys of <see cref="SponsorKeys"/>.</summary>
public static class SponsorOfferCodes
{
    /// <summary>Inbox kind of a sponsor's renewal offer, a decision with two options.</summary>
    public const string OfferKind = "sponsor.offer";

    /// <summary>The item argument that names the offer.</summary>
    public const string OfferArgument = "offer";

    public const string OptionAccept = "accept";

    public const string OptionDecline = "decline";
}

/// <summary>
/// The owner of the <see cref="SponsorOfferCodes.OfferKind"/> inbox decision: a sponsor's offer to renew a deal that is about to end (#254).
/// The player answers in the inbox, and if nobody answers by the day the deal ends, the offer is declined and the sponsor moves on.
/// Both answers end in <see cref="SponsorOfferAnswer"/>, the same code the sponsors screen and the AI teams use.
/// </summary>
public sealed class SponsorOfferResolver : IInboxResolver
{
    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;

    public SponsorOfferResolver(SponsorBook book, SponsorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public string Kind => SponsorOfferCodes.OfferKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId is not (SponsorOfferCodes.OptionAccept or SponsorOfferCodes.OptionDecline))
        {
            return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }

        var offer = OfferOf(item);
        if (offer is null)
        {
            return TranslationMessage.Of(SponsorKeys.UnknownOffer);
        }

        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var declining = optionId == SponsorOfferCodes.OptionDecline;
        if (!offer.IsOpen || (!declining && today > offer.ValidUntil))
        {
            return TranslationMessage.Of(SponsorKeys.OfferClosed);
        }

        return null;
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var offer = OfferOf(item) ?? throw new InvalidOperationException("Execute ran for an item that should have been rejected.");
        return SponsorOfferAnswer.Apply(
            _book,
            _environment,
            new ManagerId(item.ManagerId),
            context.World.CurrentDate,
            offer.Id,
            optionId == SponsorOfferCodes.OptionAccept);
    }

    private SponsorOffer? OfferOf(InboxItem item) =>
        item.Arguments.TryGetValue(SponsorOfferCodes.OfferArgument, out var id) ? _book.Section.FindOffer(id) : null;
}
