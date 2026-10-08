using Paddock.Application.Contracts;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>#265: what the player sees of a contract (the end year, the waiting item, pre-contracts, pronouns) and the people lists.</summary>
public sealed class PeopleContractsTests
{
    private static string Iso(GameDate date) => date.ToString();

    [Fact]
    public void AnOfferGivesTheManagerAWaitingItemWithTheDayTheAnswerIsDue()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);

        lab.Offer(Anna, id, Terms(60_000));

        var respondOn = lab.Find(id).RespondOn!.Value;
        var item = Assert.Single(lab.Inbox.Section.ItemsOf(Anna.Value), entry => entry.SubjectKey == ContractKeys.InboxWaitingSubject);
        Assert.False(item.NeedsDecision);
        Assert.Equal(Iso(respondOn), item.Arguments["until"]);
        Assert.Equal(id, item.Arguments[ContractEngine.NegotiationArgument]);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnAiProposerGetsNoWaitingItem()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Bot, TeamC, DriverX);
        lab.Offer(Bot, id, Terms(60_000));
        Assert.Empty(lab.Inbox.Section.ItemsOf(Bot.Value));
    }

    [Fact]
    public void AProfessionalPersonAnswersSoonerThanACarelessOneForTheSameOffer()
    {
        var professional = new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 10, 10, 20, 10);
        var careless = new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 10, 10, 1, 10);
        for (var round = 1; round <= 8; round++)
        {
            var key = "delay:neg:" + round + ":1";
            var quick = NegotiationCore.ResponseDelayDays(professional, key);
            var slow = NegotiationCore.ResponseDelayDays(careless, key);
            Assert.InRange(quick, NegotiationEstimates.ResponseDelayMinDays, NegotiationEstimates.ResponseDelayMinDays + NegotiationEstimates.ResponseDelayJitterDays);
            Assert.True(slow > quick, "careless " + slow + " professional " + quick);
            Assert.Equal(quick, NegotiationCore.ResponseDelayDays(professional, key));
        }
    }

    [Fact]
    public void TheAnswerDayOfAnOfferFollowsThePerson()
    {
        var labQuick = new Lab();
        labQuick.Personality.Set(DriverX, new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 10, 10, 20, 10));
        var quick = labQuick.OpenOk(Anna, TeamA, DriverX);
        labQuick.Offer(Anna, quick, Terms(60_000));

        var labSlow = new Lab();
        labSlow.Personality.Set(DriverX, new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 10, 10, 1, 10));
        var slow = labSlow.OpenOk(Anna, TeamA, DriverX);
        labSlow.Offer(Anna, slow, Terms(60_000));

        Assert.True(labSlow.Find(slow).RespondOn > labQuick.Find(quick).RespondOn);
    }

    [Fact]
    public void ADriverWhoseContractEndsThisYearCanBeSignedNowForNextSeason()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, Veteran);
        lab.Offer(Anna, id, Terms(130_000));
        lab.AdvanceUntilAnswered(id);

        // The negotiation already says when the contract would start: the day after the current one ends.
        var view = lab.Query.View(Paddock.Application.Access.AccessContext.ForManager(new Paddock.Application.Access.ManagerId(Anna.Value))).Items.Single(item => item.Id == id);
        Assert.Equal(new DateOnly(1956, 1, 1), view.StartsOn);
        Assert.Equal(Veteran, view.Person);
        Assert.True(view.SalaryGuide!.Min < view.SalaryGuide.Suggested && view.SalaryGuide.Suggested < view.SalaryGuide.Max);

        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(lab.Accept(Anna, id));

        var profile = DriverProfileRead.Of(lab.World, TeamA, lab.Today, Veteran.Value);
        Assert.False(profile.Own);
        Assert.Equal("1957-12-31", profile.ContractEnd);
        Assert.Equal("1956-01-01", profile.UpcomingStart);
        Assert.NotNull(profile.Upcoming);
        Assert.Equal("Fixture fixture_team_a", profile.UpcomingOrganizationName);
    }

    [Fact]
    public void AcceptingTheTermsOfARenewalMovesTheContractEndShownInTheProfile()
    {
        // The 1955 bug: a renewal was signed, but the profile kept showing the contract that ends this year.
        var lab = new Lab();
        var current = lab.World.Contracts.Single(contract => contract.PersonId == Veteran);
        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(lab.Submit(new RenewContractCommand
        {
            ManagerId = Bram,
            IssuedOn = Date(lab.Today),
            Contract = current.Id,
            Offer = Terms(130_000, years: 3),
        }));
        var id = lab.Section.Negotiations.Single().Id;
        Assert.Equal(NegotiationStatus.PersonAgreed, lab.AdvanceUntilAnswered(id).Status);
        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(lab.Accept(Bram, id));

        var profile = DriverProfileRead.Of(lab.World, TeamB, lab.Today, Veteran.Value);
        Assert.True(profile.Own);
        Assert.Equal("1955-12-31", profile.Contract!.End);
        Assert.Equal("1958-12-31", profile.ContractEnd);
        Assert.Equal("1958-12-31", profile.Upcoming!.End);
        Assert.Equal("1956-01-01", profile.UpcomingStart);
    }

    [Fact]
    public void TheAcceptLabelFollowsTheGenderOfThePerson()
    {
        var woman = PersonId.Real("fixture_driver_fia");
        var lab = new Lab(customize: world =>
        {
            (world, _) = world.AddPerson(new PersonSpec("Fia", "Flame", new GameDate(1930, 1, 1), "GBR", true, woman.Value, [PersonRole.Driver], Truth(12), isFemale: true));
            foreach (var team in new[] { TeamA, TeamB, TeamC })
            {
                world = world.SetKnowledge(new PersonKnowledge(
                    team,
                    woman,
                    Truth(12).Attributes.Select(attribute => new KnownAttribute(attribute.Key, new AttributeBand(attribute.Value - 1, attribute.Value + 1))).ToArray(),
                    null));
            }

            return world;
        });

        Assert.Equal(ContractKeys.InboxAcceptLabelFemale, AcceptLabelAfterALowOffer(lab, woman));
        Assert.Equal(ContractKeys.InboxAcceptLabelMale, AcceptLabelAfterALowOffer(new Lab(), DriverX));
    }

    private static string AcceptLabelAfterALowOffer(Lab lab, PersonId person)
    {
        var id = lab.OpenOk(Anna, TeamA, person);
        lab.Offer(Anna, id, Terms(60_000));
        Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(id).Status);
        var item = lab.Inbox.Section.ItemsOf(Anna.Value).Single(entry => entry.Kind == ContractEngine.ResponseKind);
        return item.Options.Single(option => option.Id == ContractEngine.OptionAccept).LabelKey;
    }

    [Fact]
    public void ThePointsBonusIsNeverOfferedByTheBridgeShape()
    {
        // The slider guide spans half to double the reference and the suggested range sits inside it.
        var guide = PeopleViews.SalaryGuide(100_000);
        Assert.Equal(50_000, guide.Min);
        Assert.Equal(100_000, guide.Suggested);
        Assert.Equal(200_000, guide.Max);
        Assert.InRange(guide.SuggestedLow, guide.Min, guide.Suggested);
        Assert.InRange(guide.SuggestedHigh, guide.Suggested, guide.Max);
    }

    [Fact]
    public void AWomanIsSavedAndLoadedAsAWoman()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("paddock-v28-").FullName, "gender.paddock");
        try
        {
            using var save = SaveFile.Create(path, WorldFixtures.Meta());
            var repo = new WorldRepository(save);
            var world = WorldFixtures.Small();
            (world, var id) = world.AddPerson(new PersonSpec("Fia", "Flame", new GameDate(1930, 1, 1), "GBR", false, null, [PersonRole.Driver], Truth(12), isFemale: true));
            repo.SaveWorld(world, WorldFixtures.Opening);
            var loaded = repo.LoadWorld();
            Assert.True(loaded.GetPerson(id).IsFemale);
            Assert.All(loaded.Persons.Where(person => person.Id != id), person => Assert.False(person.IsFemale));
            Assert.Equal(world.StateHash(), loaded.StateHash());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void AWorldWithNoWomenKeepsItsHash()
    {
        var world = WorldFixtures.Small();
        var woman = world.AddPerson(new PersonSpec("Fia", "Flame", new GameDate(1930, 1, 1), "GBR", false, null, [PersonRole.Driver], Truth(12), isFemale: true)).State;
        var man = world.AddPerson(new PersonSpec("Fia", "Flame", new GameDate(1930, 1, 1), "GBR", false, null, [PersonRole.Driver], Truth(12))).State;
        Assert.NotEqual(woman.StateHash(), man.StateHash());
    }

    [Fact]
    public void TheMarketShowsTheCurrentPayOfAContractAndTheLastPayOfAFreeAgent()
    {
        var lab = new Lab();
        var market = MarketRead.Of(Paddock.Application.Access.AccessContext.ForManager(new Paddock.Application.Access.ManagerId(Anna.Value)), lab.Book, TeamA, lab.Today);

        // The veteran is under contract at team B for 100 000: that is the pay shown, not what team A thinks he is worth.
        Assert.Equal(100_000, market.Contracted.Single(person => person.PersonId == Veteran.Value).Salary);
        // A free agent who never had a contract has no pay to show.
        Assert.Equal(0, market.FreeAgents.First(person => person.PersonId == DriverX.Value).Salary);
    }

    [Fact]
    public void WhileTheCommercialDirectorIsHiddenEveryTeamCountsWithTheSameSponsorSkill()
    {
        var lab = new Lab();
        var skills = new Paddock.Application.Sponsors.KnownNegotiatorSkills();

        Assert.Equal(Paddock.Domain.Sponsors.SponsorEstimates.NeutralSkill, skills.Skill(lab.World, TeamA, lab.Today));
        Assert.Equal(skills.Skill(lab.World, TeamA, lab.Today), skills.Skill(lab.World, TeamB, lab.Today));
        Assert.Contains(StaffRole.CommercialDirector, StaffCatalogue.HiddenRoles);
    }
}
