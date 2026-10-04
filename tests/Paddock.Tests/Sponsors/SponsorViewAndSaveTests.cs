using Paddock.Application.Access;
using Paddock.Application.Sponsors;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Sponsors;

public class SponsorViewAndSaveTests : IDisposable
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-sponsors-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AManagerSeesOwnSlotsDealsTalksAndObjectivesAndAnotherTeamIsUnknown()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var deal = kit.SignDeal("vestoil_works", 1, Opening);
        var talk = kit.OpenTalk("dunmore_tyres", 2, Opening);
        kit.Live(Opening, 3);
        var anna = AccessContext.ForManager(new AccessManagerId("human:anna"));

        var own = Assert.IsType<SponsorView.Own>(SponsorQuery.Read(anna, SponsorKit.Alfa, kit.Book, kit.Environment, kit.ObjectiveQuery(), new GameDate(1955, 1, 4)));

        Assert.Equal(SponsorEstimates.SlotsPerTeam, own.Slots.Count);
        Assert.Equal(deal.Id, own.Slots[0].DealId);
        Assert.Equal(talk.Id, own.Slots[1].TalkId);
        Assert.Empty(own.Slots[0].Candidates);
        Assert.True(own.Slots[2].Candidates.Count >= SponsorEstimates.MinCandidatesPerSlot);
        Assert.Contains(own.Slots[2].Candidates, candidate => candidate.SponsorId == "vestoil_works" && candidate.Blocked is not null);
        Assert.Contains(own.Slots[2].Candidates, candidate => candidate.SponsorId == "rheinwerk_motoren" && candidate.Blocked is null);
        var dealView = Assert.Single(own.Deals);
        Assert.Equal("Vestoil Works", dealView.SponsorName);
        Assert.Equal(deal.AnnualCents, dealView.AnnualCents);
        var objective = Assert.Single(own.Objectives);
        Assert.Equal(deal.ObjectiveId, objective.Id);
        Assert.Equal(SponsorKeys.ObjectiveReason, objective.Why.Reason.Key);
        Assert.NotNull(objective.Forecast);
        var talkView = Assert.Single(own.Talks);
        Assert.True(talkView.CurrentAnnualCents <= talkView.CappedAnnualCents);

        var other = SponsorQuery.Read(anna, SponsorKit.Beta, kit.Book, kit.Environment, kit.ObjectiveQuery(), Opening);
        Assert.Equal(SponsorKeys.ViewUnknown, Assert.IsType<SponsorView.Unknown>(other).Reason.Key);
        Assert.IsType<SponsorView.Own>(SponsorQuery.Read(AccessContext.Developer, SponsorKit.Beta, kit.Book, kit.Environment, kit.ObjectiveQuery(), Opening));
    }

    [Fact]
    public void TheHiddenRivalIsOnlyShownToANegotiatorSkilledEnoughAndTheViewChangesNothing()
    {
        bool? Seen(int skill)
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var kit = new SponsorKit(Opening, seed: seed, skill: skill);
                kit.OpenTalk("vestoil_works", 1, Opening);
                kit.Live(Opening, 2);
                var talk = kit.Book.Section.Talks[0];
                if (talk.Rival != RivalState.Present)
                {
                    continue;
                }

                var before = kit.World.StateHash();
                var anna = AccessContext.ForManager(new AccessManagerId("human:anna"));
                var view = Assert.IsType<SponsorView.Own>(SponsorQuery.Read(anna, SponsorKit.Alfa, kit.Book, kit.Environment, kit.ObjectiveQuery(), new GameDate(1955, 1, 3)));
                Assert.Equal(before, kit.World.StateHash());
                Assert.All(kit.LastState!.Value.RngStates.Keys, slot => Assert.Equal("Market", slot.Name));
                return Assert.Single(view.Talks).RivalKnown;
            }

            throw new InvalidOperationException("No seed produced a rival.");
        }

        Assert.Null(Seen(0));
        Assert.True(Seen(SponsorEstimates.RivalInsightSkill));
    }

    [Fact]
    public void SponsorsAndObjectivesRoundTripThroughTheSaveWithTheSameHash()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        kit.SignDeal("vestoil_works", 1, Opening);
        kit.SignDeal("rheinwerk_motoren", 2, Opening);
        kit.OpenTalk("dunmore_tyres", 3, Opening);
        kit.OpenTalk("pellegrini_gomme", 1, Opening, SponsorKit.Beta);
        kit.Podiums = 1;
        kit.Live(Opening, 366);
        var world = kit.World.WithDate(new GameDate(1956, 1, 2));
        Assert.NotEmpty(kit.Book.Section.Offers);
        Assert.NotEmpty(kit.Book.Section.TrustRows());

        using var file = SaveFile.Create(Path.Combine(_directory, "sponsors.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, new GameDate(1956, 1, 2));
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var sponsors = loaded.Section<SponsorsSection>(SponsorsSection.SectionName)!;
        Assert.Equal(kit.Book.Section.Deals.Count, sponsors.Deals.Count);
        Assert.Equal(kit.Book.Section.Next, sponsors.Next);
        var objectives = loaded.Section<ObjectivesSection>(ObjectivesSection.SectionName)!;
        Assert.Equal(kit.Book.Objectives.Objectives.Count, objectives.Objectives.Count);
        Assert.Equal(
            kit.Book.Objectives.Objectives.Select(item => item.Predicate).ToArray(),
            objectives.Objectives.Select(item => item.Predicate).ToArray());
    }

    [Fact]
    public void ASaveWithoutSponsorsStillLoadsAsNoSection()
    {
        var world = WorldFixtures.Small();
        using var file = SaveFile.Create(Path.Combine(_directory, "plain.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);

        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        Assert.Null(loaded.Section<SponsorsSection>(SponsorsSection.SectionName));
        Assert.Null(loaded.Section<ObjectivesSection>(ObjectivesSection.SectionName));
    }

    [Fact]
    public void SponsorRecordNumbersAreNeverReusedAndRestoreChecksTheCounter()
    {
        var kit = new SponsorKit(Opening, skill: 20);
        var first = kit.OpenTalk("vestoil_works", 1, Opening);
        var deal = kit.SignDeal("corvane_fuels", 2, Opening);

        Assert.NotEqual(first.Number, deal.Number);
        Assert.Throws<InvalidOperationException>(() => SponsorsSection.Restore(1, kit.Book.Section.Talks, kit.Book.Section.Deals, [], [], []));
    }
}
