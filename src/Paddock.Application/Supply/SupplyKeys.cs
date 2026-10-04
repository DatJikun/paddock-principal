using Paddock.Application.Localization;

namespace Paddock.Application.Supply;

/// <summary>
/// Player-facing supply keys. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c> under the <c>supply.</c> prefix.
/// The reasons a supplier states are <see cref="Paddock.Domain.Supply.SupplyReasons"/> plus the shared <c>negotiation.reason.*</c> keys.
/// Item and kind names are built from a prefix and the enum name (<see cref="ItemName"/>, <see cref="KindName"/>); they are listed
/// here so the i18n check sees them.
/// </summary>
public static class SupplyKeys
{
    [TranslationKey]
    public const string NotYourOrganization = "supply.error.notYourOrganization";

    [TranslationKey]
    public const string UnknownOrganization = "supply.error.unknownOrganization";

    [TranslationKey]
    public const string NoBooks = "supply.error.noBooks";

    [TranslationKey]
    public const string NotATeam = "supply.error.notATeam";

    [TranslationKey]
    public const string UnknownSupplier = "supply.error.unknownSupplier";

    [TranslationKey]
    public const string SupplierCannotSupply = "supply.error.supplierCannotSupply";

    [TranslationKey]
    public const string WorksNotOffered = "supply.error.worksNotOffered";

    [TranslationKey]
    public const string LastYearEnginesOnly = "supply.error.lastYearEnginesOnly";

    [TranslationKey]
    public const string FuelNotChoosable = "supply.error.fuelNotChoosable";

    [TranslationKey]
    public const string BadTerms = "supply.error.badTerms";

    [TranslationKey]
    public const string BadSeason = "supply.error.badSeason";

    [TranslationKey]
    public const string AlreadySupplied = "supply.error.alreadySupplied";

    [TranslationKey]
    public const string TalksOpen = "supply.error.talksOpen";

    [TranslationKey]
    public const string UnknownNegotiation = "supply.error.unknownNegotiation";

    [TranslationKey]
    public const string NegotiationClosed = "supply.error.negotiationClosed";

    [TranslationKey]
    public const string NotCountered = "supply.error.notCountered";

    [TranslationKey]
    public const string NoRounds = "supply.error.noRounds";

    [TranslationKey]
    public const string ReasonFee = "supply.reason.fee";

    [TranslationKey]
    public const string ReasonSale = "supply.reason.sale";

    [TranslationKey]
    public const string ReasonPriceTooLow = "supply.reason.price_too_low";

    [TranslationKey]
    public const string ReasonNoCapacity = "supply.reason.no_capacity";

    [TranslationKey]
    public const string ReasonExclusiveTaken = "supply.reason.exclusive_taken";

    [TranslationKey]
    public const string ReasonExclusiveUnavailable = "supply.reason.exclusive_unavailable";

    [TranslationKey]
    public const string InboxSignedSubject = "supply.inbox.signed.subject";

    [TranslationKey]
    public const string InboxCounteredSubject = "supply.inbox.countered.subject";

    [TranslationKey]
    public const string InboxRefusedSubject = "supply.inbox.refused.subject";

    [TranslationKey]
    public const string InboxLapsedSubject = "supply.inbox.lapsed.subject";

    [TranslationKey]
    public const string InboxEndedSubject = "supply.inbox.ended.subject";

    [TranslationKey]
    public const string ItemEngine = "supply.item.engine";

    [TranslationKey]
    public const string ItemTyres = "supply.item.tyres";

    [TranslationKey]
    public const string ItemFuel = "supply.item.fuel";

    [TranslationKey]
    public const string KindWorks = "supply.kind.works";

    [TranslationKey]
    public const string KindPartner = "supply.kind.partner";

    [TranslationKey]
    public const string KindCustomer = "supply.kind.customer";

    [TranslationKey]
    public const string KindLastYearEngine = "supply.kind.lastYearEngine";

    /// <summary>Inbox kind of every supply notice. A stable code, not text.</summary>
    public const string InboxKind = "supply.notice";

    public static string ItemName(Paddock.Domain.Supply.SupplyItem item) => item switch
    {
        Paddock.Domain.Supply.SupplyItem.Engine => ItemEngine,
        Paddock.Domain.Supply.SupplyItem.Tyres => ItemTyres,
        _ => ItemFuel,
    };

    public static string KindName(Paddock.Domain.Supply.SupplyKind kind) => kind switch
    {
        Paddock.Domain.Supply.SupplyKind.Works => KindWorks,
        Paddock.Domain.Supply.SupplyKind.Partner => KindPartner,
        Paddock.Domain.Supply.SupplyKind.Customer => KindCustomer,
        _ => KindLastYearEngine,
    };
}
