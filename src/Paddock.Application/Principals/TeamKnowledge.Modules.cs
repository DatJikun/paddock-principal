using Paddock.Application.Cars;
using Paddock.Application.Development;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Principals;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;

namespace Paddock.Application.Principals;

internal sealed partial class TeamKnowledge
{
    /// <summary>ESTIMATE: day of the year from which the principal looks for the engine of the season after next (about a third of the year to go).</summary>
    public const int SupplyLeadDayOfYear = 244;

    private static readonly SupplyKind[] OfferedKinds = [SupplyKind.Partner, SupplyKind.Customer, SupplyKind.LastYearEngine];

    // ---------------------------------------------------------------- development (T42)

    /// <summary>The own plan, car and standings as the engineers show them. Null when the host did not give development, or the team has no car or plan view.</summary>
    public DevelopmentInput? Development(AiPrincipalRecord? record)
    {
        var sources = _env.Development;
        if (sources is null)
        {
            return null;
        }

        var owned = new DevelopmentQuery(sources.Book, sources.Environment).ViewOf(Access, Organization);
        if (owned is not { } team)
        {
            return null;
        }

        var own = team.View;
        var car = sources.Cars.Section.Of(Organization).FirstOrDefault();
        if (car is null)
        {
            return null;
        }

        // same bias key as CarQuery, so a principal reads the bands its own car screen would show
        var key = car.Organization.Value + "|" + car.Id + "|" + car.Season.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var downforce = Band(car.Levels.Downforce, team.Vision, team.Aero, key);
        var grip = Band(car.Levels.MechanicalGrip, team.Vision, team.Aero, key);
        var reliability = Band(car.Levels.Reliability, team.Vision, team.Aero, key);
        var braking = Band(car.Levels.Braking, team.Vision, team.Aero, key);

        var concepts = own.Projects
            .Where(project => project.Kind == DevKindNames.Concept && project.Status is "Active" or "Ready" && project.Timing is not "Hold")
            .Select(project => new ConceptCase(
                project.ProjectId,
                project.Status == "Ready",
                project.Timing,
                (project.ExpectedGain.Low + project.ExpectedGain.High) / 2.0,
                project.ProductionDays ?? 0))
            .ToArray();
        var year = Today.Year;
        var result = new DevelopmentInput(
            Day,
            new DevelopmentPlanView(own.CurrentPercent, own.AccountPercent, own.NextYearPercent, own.AeroPriority, own.ChassisPriority, own.ReliabilityPriority, own.TyresPriority),
            Mid(downforce),
            Mid(grip),
            Mid(reliability),
            Mid(braking),
            Position(year - 1),
            Position(year - 2),
            Position(year),
            TeamCount(),
            _env.Outlook.ChangeAfter(year),
            record?.SacrificedSeason ?? 0,
            Funds().Headroom < 0,
            concepts);
        return result;
    }

    private static double Mid(CarBandView band) => (band.Low + band.High) / 2.0;

    private static CarBandView Band(double truth, int vision, int aero, string biasKey)
    {
        var band = CarKnowledgeBands.Around(truth, vision, aero, biasKey);
        return new CarBandView(band.Low, band.High);
    }

    // ---------------------------------------------------------------- supply (T43)

    /// <summary>What the team needs from a supplier, the market's public offers, and its own supply talks. Null when the host did not give supply.</summary>
    public SupplyBundle? Supply()
    {
        var sources = _env.Supply;
        if (sources is null)
        {
            return null;
        }

        var view = new SupplyQuery(sources.Book, sources.Environment).View(Access);
        var year = Today.Year;
        var engineDeals = view.Deals.Where(deal => deal.Item == SupplyItem.Engine && deal.Status == SupplyDealStatus.Active).ToArray();
        bool Covers(int season) => engineDeals.Any(deal => deal.FirstSeason <= season && season <= deal.LastSeason);
        var inForce = engineDeals.FirstOrDefault(deal => deal.FirstSeason <= year && year <= deal.LastSeason);
        var lead = new DateOnly(year, 1, 1).AddDays(SupplyLeadDayOfYear - 1);

        int? firstSeason = null;
        if (!Covers(year))
        {
            firstSeason = year;
        }
        else if (!Covers(year + 1) && Day >= lead)
        {
            firstSeason = year + 1;
        }

        var events = new List<DateOnly>();
        if (firstSeason is null && !Covers(year + 1) && Day < lead)
        {
            events.Add(lead);
        }

        var season = firstSeason ?? year;
        var referenceBudget = sources.Environment.Eras.ReferenceBudgetCents(season);
        var refused = view.Talks
            .Where(talk => talk.Item == SupplyItem.Engine && talk.Status is NegotiationStatus.Refused or NegotiationStatus.WalkedAway or NegotiationStatus.Lapsed)
            .Select(talk => talk.SupplierId)
            .ToHashSet(StringComparer.Ordinal);
        var quotes = new List<SupplyQuote>();
        var supply = sources.Book.Section;
        foreach (var supplier in World.Organizations.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            if (supplier.Id == Organization || supplier.Dissolved is not null || !SellsEngines(sources, supplier, season))
            {
                continue;
            }

            var form = FormOf(supply, supplier.Id, season);
            foreach (var kind in OfferedKinds)
            {
                quotes.Add(new SupplyQuote(
                    supplier.Id.Value,
                    kind.ToString(),
                    SupplyPricing.ReferenceCents(SupplyItem.Engine, kind, referenceBudget),
                    SupplyEstimates.LagSeasons(kind),
                    form));
            }
        }

        var needs = new List<SupplyNeed>();
        if (firstSeason is int first)
        {
            var open = view.Talks.Any(talk => talk.Item == SupplyItem.Engine && talk.Status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered);
            var offers = open ? [] : quotes.Where(quote => !refused.Contains(quote.SupplierId)).ToArray();
            needs.Add(new SupplyNeed(SupplyItem.Engine.ToString(), first, inForce?.SupplierId, offers));
        }

        var talks = new List<SupplyTalk>();
        foreach (var talk in view.Talks.Where(item => item.Item == SupplyItem.Engine && item.Status is NegotiationStatus.AwaitingResponse or NegotiationStatus.Countered))
        {
            var quote = quotes.FirstOrDefault(item => item.SupplierId == talk.SupplierId && item.Kind == talk.Kind.ToString());
            var state = talk.Status == NegotiationStatus.Countered ? SupplyTalkState.Countered : SupplyTalkState.Awaiting;
            talks.Add(new SupplyTalk(
                talk.NegotiationId,
                talk.SupplierId,
                talk.Item.ToString(),
                talk.Kind.ToString(),
                0,
                state,
                talk.OfferCents,
                talk.CounterCents ?? 0,
                0,
                talk.RoundsLeft,
                quote?.LagSeasons ?? SupplyEstimates.LagSeasons(talk.Kind),
                quote?.FormScore,
                inForce?.SupplierId));
            if (state == SupplyTalkState.Awaiting)
            {
                events.Add(Day.AddDays(2));
            }
        }

        var headroom = Funds().Headroom * 100;
        var input = new SupplyInput(
            Day,
            Funds().AnnualBudgetDollars * 100,
            headroom,
            SupplyEstimates.YearsDiscountMilli,
            SupplyEstimates.YearsDiscountCapMilli,
            needs,
            talks);
        return new SupplyBundle(input, events);
    }

    private static bool SellsEngines(SupplySources sources, Organization supplier, int season) =>
        supplier.Kind == OrganizationKind.EngineSupplier
        || (supplier.Kind == OrganizationKind.Team && sources.Environment.Programmes.Sells(supplier.Id, season));

    /// <summary>How well a supplier's customers do: the public standings of the teams that race its engine, from -1 (bottom) to 1 (top). Null when nothing is known.</summary>
    private double? FormOf(SupplySection supply, OrganizationId supplier, int season)
    {
        var customers = supply.ServedBy(supplier, SupplyItem.Engine, new GameDate(season, 1, 1));
        var positions = new List<int>();
        foreach (var deal in customers)
        {
            if (_env.StandingsOrDefault.Position(deal.Customer, season - 1) is int position)
            {
                positions.Add(position);
            }
        }

        if (positions.Count == 0)
        {
            return null;
        }

        var half = Math.Max(1.0, TeamCount() / 2.0);
        return Math.Clamp((half - positions.Average()) / half, -1.0, 1.0);
    }

    // ---------------------------------------------------------------- sponsors (T38)

    /// <summary>The own sponsor slots, talks and offers as the commercial side shows them. Null when the host did not give sponsors.</summary>
    public SponsorBundle? Sponsors()
    {
        var sources = _env.Sponsors;
        if (sources is null)
        {
            return null;
        }

        var view = SponsorQuery.Read(Access, Organization, sources.Book, sources.Environment, sources.Objectives, Today);
        if (view is not SponsorView.Own own)
        {
            return null;
        }

        var events = new List<DateOnly>();
        var slots = own.Slots
            .Where(slot => slot.DealId is null && slot.TalkId is null)
            .Select(slot => new SponsorSlotCase(
                slot.Slot,
                slot.Candidates
                    .Where(candidate => candidate.Blocked is null)
                    .Select(candidate => new SponsorCandidate(candidate.SponsorId, candidate.IndicativeAnnualCents))
                    .ToArray()))
            .ToArray();
        var reference = slots.SelectMany(slot => slot.Candidates).Select(candidate => candidate.IndicativeCents).DefaultIfEmpty(0L).Max();
        var talks = own.Talks
            .Select(talk => new SponsorTalkCase(talk.Id, talk.SponsorId, talk.CurrentAnnualCents, talk.CappedAnnualCents))
            .ToArray();
        var offers = own.Offers
            .Select(offer => new SponsorOfferCase(offer.Id, offer.SponsorName, offer.AnnualCents, reference > 0 ? reference : offer.AnnualCents))
            .ToArray();
        foreach (var deal in own.Deals)
        {
            events.Add(deal.End.AddDays(-SponsorEstimates.RenewalLeadDays + 1));
        }

        foreach (var offer in own.Offers)
        {
            events.Add(offer.ValidUntil);
        }

        var input = new SponsorInput(Day, Funds().AnnualBudgetDollars * 100, slots, talks, offers);
        return new SponsorBundle(input, own.Talks.Count > 0, events);
    }

    // ---------------------------------------------------------------- scouting (T40)

    /// <summary>How many young drivers are in the talent pool, or 0 when the host did not give the pool.</summary>
    public int PoolSize() => _env.Pool?.Section.Members.Count ?? 0;
}

internal sealed record SupplyBundle(SupplyInput Input, IReadOnlyList<DateOnly> Events);

internal sealed record SponsorBundle(SponsorInput Input, bool TalkOpen, IReadOnlyList<DateOnly> Events);

/// <summary>Names of the development kinds as the query writes them (kept as text so the glue does not read the project type).</summary>
internal static class DevKindNames
{
    public const string Concept = "Concept";
}
