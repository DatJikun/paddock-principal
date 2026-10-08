using Paddock.Application.Sponsors;
using Paddock.Data.Authored;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;

namespace Paddock.Tests.Sponsors;

/// <summary>The authored sponsors file and the era rules, on the real catalog and era data. Every number in it is an ESTIMATE.</summary>
public class SponsorDataTests
{
    private static readonly SponsorKit Kit = new(new GameDate(1955, 1, 1));

    [Fact]
    public void TheAuthoredFileIsValidAgainstTheEraData()
    {
        var errors = SponsorsValidator.Validate(Kit.SponsorsFile, Kit.Data.EraPeriods);

        Assert.Empty(errors);
        Assert.Contains("ESTIMATE", Kit.SponsorsFile.Notes);
        Assert.All(Kit.SponsorsFile.Sponsors, sponsor => Assert.Equal("estimate", sponsor.Confidence));
    }

    [Fact]
    public void In1955ThereIsNoLiverySlotAndEverySlotHasAtLeastThreeCandidates()
    {
        var era = SponsorEra.ForYear(Kit.Data.EraPeriods, 1955);
        var catalog = SponsorsLoader.ToCatalog(Kit.SponsorsFile);

        Assert.False(era.Livery);
        Assert.Equal(SponsorEstimates.SlotsPerTeam, era.Slots.Count);
        Assert.All(era.Slots, kind => Assert.Equal(SlotKind.Technical, kind));
        Assert.DoesNotContain(era.Slots, SlotKinds.IsLivery);
        foreach (var kind in era.Slots)
        {
            Assert.True(catalog.Candidates(1955, kind, era).Count >= SponsorEstimates.MinCandidatesPerSlot);
        }

        Assert.Empty(catalog.Candidates(1955, SlotKind.Main, era));
        Assert.Empty(catalog.Candidates(1955, SlotKind.Secondary, era));
    }

    [Fact]
    public void In1969AMainSlotExistsAndTobaccoIsAllowed()
    {
        var era = SponsorEra.ForYear(Kit.Data.EraPeriods, 1969);
        var catalog = SponsorsLoader.ToCatalog(Kit.SponsorsFile);

        Assert.True(era.Livery);
        Assert.Contains(SlotKind.Main, era.Slots);
        Assert.Equal(SponsorEstimates.SlotsPerTeam, era.Slots.Count);
        var main = catalog.Candidates(1969, SlotKind.Main, era);
        Assert.True(main.Count >= SponsorEstimates.MinCandidatesPerSlot);
        Assert.Contains(main, sponsor => sponsor.Industry == SponsorIndustries.Tobacco);
        Assert.DoesNotContain(catalog.Candidates(1969, SlotKind.Technical, era), sponsor => !SponsorIndustries.IsTechnical(sponsor.Industry));
    }

    [Fact]
    public void TobaccoLeavesTheMarketWhenTheEraBansIt()
    {
        var catalog = SponsorsLoader.ToCatalog(Kit.SponsorsFile);
        var era = SponsorEra.ForYear(Kit.Data.EraPeriods, 2010);

        Assert.False(era.Tobacco);
        Assert.DoesNotContain(catalog.Candidates(2010, SlotKind.Main, era), sponsor => sponsor.Industry == SponsorIndustries.Tobacco);
        Assert.True(catalog.Candidates(2010, SlotKind.Main, era).Count >= SponsorEstimates.MinCandidatesPerSlot);
    }

    [Fact]
    public void TheValidatorReportsEveryProblemItFinds()
    {
        var good = Kit.SponsorsFile.Sponsors[0];
        var bad = new SponsorsFile(
            "no marker",
            [
                good,
                good with { Industry = "unobtainium" },
                good with { Id = "livery_in_technical", Industry = SponsorIndustries.Tobacco, Slots = ["technical"] },
                good with { Id = "odd_numbers", BudgetLevel = 4.0 },
                good with { Id = "odd_window", From = 1920 },
                good with { Id = "odd_objective", Objective = new SponsorObjectiveEntry("podiums_at_least", "zero", 10) },
            ]);

        var codes = SponsorsValidator.Validate(bad, Kit.Data.EraPeriods).Select(error => error.Code).ToHashSet();

        Assert.Contains(SponsorsValidator.NotEstimate, codes);
        Assert.Contains(SponsorsValidator.DuplicateId, codes);
        Assert.Contains(SponsorsValidator.BadIndustry, codes);
        Assert.Contains(SponsorsValidator.BadSlots, codes);
        Assert.Contains(SponsorsValidator.BadNumber, codes);
        Assert.Contains(SponsorsValidator.BadWindow, codes);
        Assert.Contains(SponsorsValidator.BadObjective, codes);
    }

    [Fact]
    public void TheValidatorEnforcesTheMinimumNumberOfCandidatesPerSlot()
    {
        var thin = new SponsorsFile(Kit.SponsorsFile.Notes, Kit.SponsorsFile.Sponsors.Where(sponsor => !sponsor.Slots.Contains("technical") || sponsor.Id is "vestoil_works" or "corvane_fuels").ToArray());

        var errors = SponsorsValidator.Validate(thin, Kit.Data.EraPeriods);

        Assert.Contains(errors, error => error.Code == SponsorsValidator.TooFewCandidates && error.Message.Contains("technical", StringComparison.Ordinal));
    }

    [Fact]
    public void PricesScaleWithTheEraBenchmarkAndPopularityAndFollowTheFormula()
    {
        var catalog = SponsorsLoader.ToCatalog(Kit.SponsorsFile);
        var vestoil = catalog.Find("vestoil_works")!;

        Assert.Equal(816_000, SponsorPricing.FullAnnualCents(vestoil, SlotKind.Technical, 60_000, 1000));
        Assert.Equal(897_600, SponsorPricing.FullAnnualCents(vestoil, SlotKind.Technical, 60_000, 1100));
        Assert.Equal(1_632_000, SponsorPricing.FullAnnualCents(vestoil, SlotKind.Technical, 120_000, 1000));
        var instalments = Enumerable.Range(1, SponsorEstimates.InstalmentsPerYear).Sum(number => SponsorPricing.InstalmentCents(1_000_003, number));
        Assert.Equal(1_000_003, instalments);
    }

    [Fact]
    public void TermsImproveWithWaitingUpToACapThatSkillRaises()
    {
        Assert.Equal(800, SponsorPricing.TermsMilli(0, SponsorPricing.CapMilli(0)));
        Assert.Equal(920, SponsorPricing.TermsMilli(20, SponsorPricing.CapMilli(0)));
        Assert.Equal(1050, SponsorPricing.TermsMilli(500, SponsorPricing.CapMilli(0)));
        Assert.Equal(1150, SponsorPricing.TermsMilli(500, SponsorPricing.CapMilli(20)));
        Assert.Equal(1, SponsorPricing.ParallelTalks(0));
        Assert.Equal(2, SponsorPricing.ParallelTalks(7));
        Assert.Equal(3, SponsorPricing.ParallelTalks(20));
    }

    [Fact]
    public void EveryTranslationKeyOfTheSponsorSystemIsInBothLanguages()
    {
        var keys = typeof(SponsorKeys).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(key => key != SponsorKeys.InboxKind)
            .ToArray();
        foreach (var language in new[] { "en", "pl" })
        {
            var text = File.ReadAllText(Path.Combine(RepoPaths.Root(), "strings", language + ".json"));
            Assert.All(keys, key => Assert.Contains("\"" + key + "\"", text, StringComparison.Ordinal));
        }

        Assert.Equal(SponsorReason.Instalment, SponsorKeys.ReasonInstalment);
        Assert.Equal(SponsorReason.Bonus, SponsorKeys.ReasonBonus);
        Assert.All(SponsorIndustries.All, industry => Assert.Contains(SponsorKeys.IndustryName(industry), keys));
    }

    [Fact]
    public void AHarderTargetPaysMoreAndANationalityObjectiveStaysAuthored()
    {
        const int field = 12;
        var position = new SponsorObjectiveSpec(SponsorObjectiveSpec.ChampionshipPositionAtMost, "6", 364);
        var podiums = new SponsorObjectiveSpec(SponsorObjectiveSpec.PodiumsAtLeast, "3", 364);
        var nationality = new SponsorObjectiveSpec(SponsorObjectiveSpec.DriverNationalityInLineup, "FRA", 364);
        int previousPositionBonus = int.MaxValue;
        int previousPodiumBonus = int.MaxValue;
        for (var expected = 1; expected <= field; expected++)
        {
            var scaledPosition = SponsorObjectiveScale.Scale(position, expected, field);
            var scaledPodiums = SponsorObjectiveScale.Scale(podiums, expected, field);
            var positionBonus = SponsorObjectiveScale.Reward(position, scaledPosition).BonusMilli;
            var podiumBonus = SponsorObjectiveScale.Reward(podiums, scaledPodiums).BonusMilli;
            Assert.True(positionBonus <= previousPositionBonus);
            Assert.True(podiumBonus <= previousPodiumBonus);
            previousPositionBonus = positionBonus;
            previousPodiumBonus = podiumBonus;
            Assert.Equal(nationality, SponsorObjectiveScale.Scale(nationality, expected, field));
        }

        var top = SponsorObjectiveScale.Scale(position, 1, field);
        var bottom = SponsorObjectiveScale.Scale(position, field, field);
        Assert.True(int.Parse(top.Value, System.Globalization.CultureInfo.InvariantCulture) < int.Parse(bottom.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(SponsorObjectiveScale.Reward(position, top).BonusMilli > SponsorObjectiveScale.Reward(position, bottom).BonusMilli);
        Assert.True(SponsorObjectiveScale.Reward(position, top).Trust > SponsorObjectiveScale.Reward(position, bottom).Trust);
        var topPodiums = int.Parse(SponsorObjectiveScale.Scale(podiums, 1, field).Value, System.Globalization.CultureInfo.InvariantCulture);
        var bottomPodiums = int.Parse(SponsorObjectiveScale.Scale(podiums, field, field).Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(topPodiums > bottomPodiums);
        Assert.Equal((SponsorEstimates.BonusMilli, SponsorEstimates.TrustOnMet), SponsorObjectiveScale.Reward(nationality, nationality));
    }
}
