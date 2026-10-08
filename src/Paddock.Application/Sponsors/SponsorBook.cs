using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Sponsors;

/// <summary>
/// The live world as sponsor commands see it: the host owns the world, this reads it and puts sections back.
/// One <see cref="Update"/> changes the sponsors, objectives and finance sections together, so a deal, its objective and its
/// first posting are never half written.
/// </summary>
public sealed class SponsorBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public SponsorBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public SponsorsSection Section => World.Section<SponsorsSection>(SponsorsSection.SectionName) ?? SponsorsSection.Empty;

    public ObjectivesSection Objectives => World.Section<ObjectivesSection>(ObjectivesSection.SectionName) ?? ObjectivesSection.Empty;

    public FinanceSection Finance => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public static SponsorBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SponsorBook(() => session.World, session.StoreWorld);
    }

    public void Write(SponsorsSection sponsors, ObjectivesSection? objectives = null, FinanceSection? finance = null)
    {
        ArgumentNullException.ThrowIfNull(sponsors);
        var world = World.WithSection(sponsors);
        if (objectives is not null)
        {
            world = world.WithSection(objectives);
        }

        if (finance is not null)
        {
            world = world.WithSection(finance);
        }

        _write(world);
    }
}

public static class SponsorEventTypes
{
    public const string TalksOpened = "sponsor.talks_opened";

    public const string DealSigned = "sponsor.deal_signed";

    public const string TalksEnded = "sponsor.talks_ended";

    public const string OfferAnswered = "sponsor.offer_answered";

    public const string TermsProposed = "sponsor.terms_proposed";

    public const string OfferCountered = "sponsor.offer_countered";
}

public sealed record SponsorTalksOpened(ManagerId ManagerId, DateOnly OccurredOn, string TalkId, string SponsorId, int Slot) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.TalksOpened;
}

public sealed record SponsorDealSigned(ManagerId ManagerId, DateOnly OccurredOn, string DealId, string SponsorId, long AnnualCents, string? ObjectiveId) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.DealSigned;
}

public sealed record SponsorTalksEnded(ManagerId ManagerId, DateOnly OccurredOn, string TalkId) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.TalksEnded;
}

public sealed record SponsorTermsProposed(ManagerId ManagerId, DateOnly OccurredOn, string TalkId, int Years, SponsorAmbition Ambition) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.TermsProposed;
}

public sealed record SponsorOfferCountered(ManagerId ManagerId, DateOnly OccurredOn, string OfferId, int Years, SponsorAmbition Ambition, long AnnualCents) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.OfferCountered;
}

public sealed record SponsorOfferAnswered(ManagerId ManagerId, DateOnly OccurredOn, string OfferId, bool Accepted, string? DealId) : IDomainEvent
{
    public string TypeId => SponsorEventTypes.OfferAnswered;
}

/// <summary>
/// Who negotiates for an organization and how well, from 0 to 20. The commercial director when the role exists that year,
/// otherwise the team principal. Only what the organization believes about its own person is read (INV-003).
/// </summary>
public interface INegotiatorSkills
{
    int Skill(WorldState world, OrganizationId organization, GameDate on);
}

/// <summary>
/// Reads the middle of the bands the organization believes. A commercial director (a role from 1968) counts as the mean of
/// <c>negotiation</c> and <c>marketing</c>; without one the principal counts as the mean of <c>negotiation</c> and
/// <c>business</c>. Zero when there is nobody or no belief.
/// </summary>
public sealed class KnownNegotiatorSkills : INegotiatorSkills
{
    public int Skill(WorldState world, OrganizationId organization, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        // The commercial director is hidden for now (#265): every team counts with an average one, so none is penalised.
        if (!StaffCatalogue.IsTeamRoster(StaffRole.CommercialDirector))
        {
            return SponsorEstimates.NeutralSkill;
        }

        var director = Read(world, organization, on, StaffRole.CommercialDirector, "negotiation", "marketing");
        return director ?? Read(world, organization, on, StaffRole.TeamPrincipal, "negotiation", "business") ?? 0;
    }

    private static int? Read(WorldState world, OrganizationId organization, GameDate on, StaffRole role, string first, string second)
    {
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization || !contract.IsActiveOn(on) || !contract.Role.IsStaff || contract.Role.StaffRole != role)
            {
                continue;
            }

            if (world.KnowledgeOf(organization, contract.PersonId) is not PersonKnowledgeView view)
            {
                return null;
            }

            var values = view.Attributes
                .Where(attribute => attribute.Key == first || attribute.Key == second)
                .Select(attribute => (attribute.Band.Low + attribute.Band.High) / 2)
                .ToArray();
            return values.Length == 0 ? null : Math.Clamp((int)Math.Round(values.Average()), 0, SponsorEstimates.MaxSkill);
        }

        return null;
    }
}

/// <summary>Era slots and legality by year.</summary>
public interface ISponsorEras
{
    SponsorEra Era(int year);
}

/// <summary>Era rules from the authored era periods.</summary>
public sealed class PeriodSponsorEras : ISponsorEras
{
    private readonly IReadOnlyList<RulePeriod> _periods;

    public PeriodSponsorEras(IReadOnlyList<RulePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = periods;
    }

    public SponsorEra Era(int year) => SponsorEra.ForYear(_periods, year);
}

/// <summary>The ports and data of the sponsor system. A host replaces the ones it has a real system for.</summary>
public sealed class SponsorEnvironment
{
    public SponsorEnvironment(
        SponsorCatalog catalog,
        ISponsorEras eras,
        IEraFinanceSource finance,
        IOrganizationControl control,
        IObjectiveFacts facts,
        IOrganizationAppealSource? appeal = null,
        INegotiatorSkills? negotiators = null,
        ITeamOutlook? outlook = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(eras);
        ArgumentNullException.ThrowIfNull(finance);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(facts);
        Catalog = catalog;
        Eras = eras;
        Finance = finance;
        Control = control;
        Facts = facts;
        Appeal = appeal ?? new NeutralAppealSource();
        Negotiators = negotiators ?? new KnownNegotiatorSkills();
        Outlook = outlook;
    }

    public SponsorCatalog Catalog { get; }

    /// <summary>
    /// True when a human runs the team. Manager ids carry their kind (<c>human:</c>, <c>ai:</c>), so this needs no registry. A team with no
    /// manager is read as an AI team.
    /// </summary>
    public bool IsPlayerTeam(OrganizationId team) =>
        Control.ManagersOf(team).Any(manager => !manager.Value.StartsWith("ai:", StringComparison.Ordinal));

    public ISponsorEras Eras { get; }

    public IEraFinanceSource Finance { get; }

    public IOrganizationControl Control { get; }

    public IObjectiveFacts Facts { get; }

    public IOrganizationAppealSource Appeal { get; }

    public INegotiatorSkills Negotiators { get; }

    /// <summary>Public strength of a team. Null keeps the authored objective, which is the base for a team with no public facts.</summary>
    public ITeamOutlook? Outlook { get; }
}

/// <summary>
/// The expected championship position of a team (1 is best) from public facts, on a date. The same blend the board uses.
/// Null means the facts are missing and the authored objective stands.
/// </summary>
public interface ITeamOutlook
{
    int FieldSize(GameDate on);

    int? ExpectedPosition(OrganizationId organization, GameDate on);
}
