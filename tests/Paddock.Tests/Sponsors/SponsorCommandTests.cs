using Paddock.Application.Commands;
using Paddock.Application.Sponsors;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.Sponsors;

/// <summary>The four sponsor commands: who may give them, what they refuse, and what they change.</summary>
public class SponsorCommandTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void ACommandFromAManagerWhoDoesNotRunTheOrganizationIsRefusedWithATranslatedReasonAndChangesNothing()
    {
        var kit = new SponsorKit(Opening);
        var before = kit.World.StateHash();

        var key = kit.Begin(SponsorKit.Bram, SponsorKit.Alfa, "vestoil_works", 1, Opening);

        Assert.Equal(SponsorKeys.NotYourOrganization, key);
        Assert.Equal(before, kit.World.StateHash());
        Assert.Equal(SponsorKeys.UnknownSponsor, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "nobody", 1, Opening));
        Assert.Equal(TranslationKeys.ManagerUnknown, kit.Begin(new Paddock.Application.Managers.ManagerId("human:ghost"), SponsorKit.Alfa, "vestoil_works", 1, Opening));
        Assert.Equal(SponsorKeys.BadSlot, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 4, Opening));
        Assert.Equal(before, kit.World.StateHash());
    }

    [Fact]
    public void AnOrganizationWithoutBooksCannotTakeSponsorMoney()
    {
        var kit = new SponsorKit(Opening, betaToo: false);

        Assert.Equal(SponsorKeys.UnknownOrganization, kit.Begin(SponsorKit.Bram, SponsorKit.Beta, "vestoil_works", 1, Opening));
    }

    [Fact]
    public void BeginningTalksOpensTheWaitingGameForOneSlot()
    {
        var kit = new SponsorKit(Opening);

        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        Assert.True(talk.IsOpen);
        Assert.Equal(SlotKind.Technical, talk.Kind);
        Assert.Equal(RivalState.Undecided, talk.Rival);
        Assert.Equal(480_000, talk.FullAnnualCents);
        Assert.Equal(384_000, talk.AnnualCentsOn(Opening));
        Assert.Equal(SponsorKeys.SlotBusy, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "corvane_fuels", 1, Opening));
    }

    [Fact]
    public void WaitingImprovesTheTermsUpToTheCap()
    {
        var kit = new SponsorKit(Opening);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        var today = talk.AnnualCentsOn(Opening);
        var later = talk.AnnualCentsOn(Opening.AddDays(20));
        var capped = talk.AnnualCentsOn(Opening.AddDays(300));

        Assert.True(today < later);
        Assert.True(later < capped);
        Assert.Equal(talk.CappedAnnualCents, capped);
        Assert.Equal(504_000, capped);
        Assert.Equal(capped, talk.AnnualCentsOn(Opening.AddDays(900)));
    }

    [Fact]
    public void TwoSponsorsOfOneIndustryCannotShareATeam()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        kit.OpenTalk("dunmore_tyres", 1, Opening);

        Assert.Equal(SponsorKeys.IndustryConflict, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "pellegrini_gomme", 2, Opening));

        // Another team is not bound by it, and a different industry is fine.
        Assert.Null(kit.Begin(SponsorKit.Bram, SponsorKit.Beta, "pellegrini_gomme", 1, Opening));
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 2, Opening));
    }

    [Fact]
    public void TheNegotiatorLimitsHowManyTalksRunAtOnce()
    {
        var weak = new SponsorKit(Opening, skill: 0);
        weak.OpenTalk("vestoil_works", 1, Opening);
        Assert.Equal(SponsorKeys.TooManyTalks, weak.Begin(SponsorKit.Anna, SponsorKit.Alfa, "corvane_fuels", 2, Opening));

        var strong = new SponsorKit(Opening, skill: 20);
        strong.OpenTalk("vestoil_works", 1, Opening);
        strong.OpenTalk("corvane_fuels", 2, Opening);
        strong.OpenTalk("rheinwerk_motoren", 3, Opening);
        Assert.Equal(3, strong.Book.Section.OpenTalksOf(SponsorKit.Alfa).Count);
    }

    [Fact]
    public void ASponsorNeedsEnoughTeamPrestige()
    {
        var kit = new SponsorKit(Opening, prestige: 0.2);

        Assert.Equal(SponsorKeys.PrestigeTooLow, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "rheinwerk_motoren", 1, Opening));
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 1, Opening));
    }

    [Fact]
    public void In1955NoLiverySponsorCanBeApproached()
    {
        var kit = new SponsorKit(Opening);

        Assert.Equal(SponsorKeys.SlotKindMismatch, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "goldmark_tobacco", 1, Opening));
        Assert.Equal(SponsorKeys.SlotKindMismatch, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "brightline_watches", 2, Opening));
    }

    [Fact]
    public void In1969AMainSlotTakesALiverySponsor()
    {
        var start = new GameDate(1969, 1, 1);
        var kit = new SponsorKit(start, skill: 20);

        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "goldmark_tobacco", 1, start));
        Assert.Equal(SlotKind.Main, kit.Book.Section.Talks[0].Kind);
        Assert.Equal(SponsorKeys.SlotKindMismatch, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 1, start));
        Assert.Equal(SponsorKeys.IndustryConflict, kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "redfern_cigarettes", 2, start));
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "vestoil_works", 3, start));
    }

    [Fact]
    public void ATobaccoSponsorIsRefusedWhereTheEraBansIt()
    {
        var start = new GameDate(1970, 1, 1);
        var kit = new SponsorKit(start);
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "goldmark_tobacco", 1, start));

        var late = new GameDate(2008, 1, 1);
        Assert.Equal(SponsorKeys.EraForbids, new SponsorKit(late).Begin(SponsorKit.Anna, SponsorKit.Alfa, "goldmark_tobacco", 1, late));
    }

    [Fact]
    public void SigningAtCurrentTermsMakesADealWithItsObjectiveAndFixesTheBonus()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var talk = kit.OpenTalk("vestoil_works", 1, Opening);

        var rejected = kit.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Bram, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = talk.Id });
        Assert.Equal(SponsorKeys.NotYourOrganization, rejected);

        var deal = kit.SignDeal("corvane_fuels", 2, Opening);
        Assert.Single(kit.Book.Section.Deals);
        Assert.Equal(DealStatus.Active, deal.Status);
        Assert.Equal(new GameDate(1955, 12, 31), deal.End);
        Assert.NotNull(deal.ObjectiveId);
        var objective = kit.Book.Objectives.Find(deal.ObjectiveId!)!;
        Assert.Equal(SponsorKit.Alfa, objective.Owner);
        Assert.Equal(OrganizationId.Real("corvane_fuels"), objective.Grantor);
        Assert.IsType<ChampionshipPositionAtMost>(objective.Predicate);
        Assert.Equal(10m, objective.Baseline);
        Assert.Equal(new GameDate(1955, 12, 31), objective.Deadline);
        Assert.Equal("sponsor.objective.onMet", objective.EffectOnMet.Key);
        Assert.Equal("504", objective.EffectOnMet.Arguments["bonus"]);
        Assert.Equal(TalkStatus.Signed, kit.Book.Section.Talks[1].Status);
        Assert.Equal(TalkStatus.Open, kit.Book.Section.FindTalk(talk.Id)!.Status);
    }

    [Fact]
    public void ASignedTalkAndAWalkedAwayTalkAreClosedForGood()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var first = kit.OpenTalk("vestoil_works", 1, Opening);
        var second = kit.OpenTalk("corvane_fuels", 2, Opening);

        Assert.Null(kit.Run(new WalkAwayFromTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = first.Id }));
        Assert.Equal(TalkStatus.WalkedAway, kit.Book.Section.FindTalk(first.Id)!.Status);
        Assert.Equal(
            SponsorKeys.TalkClosed,
            kit.Run(new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = first.Id }));
        Assert.Equal(
            SponsorKeys.TalkClosed,
            kit.Run(new WalkAwayFromTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = first.Id }));
        Assert.Equal(
            SponsorKeys.UnknownTalk,
            kit.Run(new WalkAwayFromTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = SponsorKit.Day(Opening), OrganizationId = SponsorKit.Alfa.Value, TalkId = "spt:99" }));

        // The freed slot can be used again, and a sponsor already under contract is off the market for everyone.
        Assert.Null(kit.Begin(SponsorKit.Anna, SponsorKit.Alfa, "dunmore_tyres", 1, Opening));
        Assert.True(kit.Book.Section.FindTalk(second.Id)!.IsOpen);
    }

    [Fact]
    public void ASponsorUnderContractWithOneTeamCannotBeApproachedByAnother()
    {
        var kit = new SponsorKit(Opening);
        kit.SignDeal("vestoil_works", 1, Opening);

        Assert.Equal(SponsorKeys.SponsorUnavailable, kit.Begin(SponsorKit.Bram, SponsorKit.Beta, "vestoil_works", 1, Opening));
    }

    [Fact]
    public void TheCommandsRoundTripThroughTheSaveCodec()
    {
        ICommand[] commands =
        [
            new BeginSponsorTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = new DateOnly(1955, 1, 1), SubmissionNumber = 1, OrganizationId = "alfa", SponsorId = "vestoil_works", Slot = 2 },
            new SignAtCurrentTermsCommand { ManagerId = SponsorKit.Anna, IssuedOn = new DateOnly(1955, 1, 9), SubmissionNumber = 2, OrganizationId = "alfa", TalkId = "spt:1" },
            new WalkAwayFromTalksCommand { ManagerId = SponsorKit.Anna, IssuedOn = new DateOnly(1955, 1, 9), SubmissionNumber = 3, OrganizationId = "alfa", TalkId = "spt:2" },
            new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = new DateOnly(1955, 11, 9), SubmissionNumber = 4, OrganizationId = "alfa", OfferId = "spo:3", Accept = true },
            new RespondToSponsorOfferCommand { ManagerId = SponsorKit.Anna, IssuedOn = new DateOnly(1955, 11, 9), SubmissionNumber = 5, OrganizationId = "alfa", OfferId = "spo:4", Accept = false },
        ];
        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }
    }
}
