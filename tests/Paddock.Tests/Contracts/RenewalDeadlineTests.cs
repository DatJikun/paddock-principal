using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// #253: on 1 July every expiring contract used to arrive as a prompt with no deadline, and the clock stayed held until the
/// player had answered all of them. Now every prompt has a deadline and a default, and none holds the clock. The fixture gives
/// team B (Bram, human) a driver, key staff, two other staff and a principal, all ending on 31 December 1955. Every threshold is an ESTIMATE.
/// </summary>
public class RenewalDeadlineTests
{
    private static readonly PersonId TechDirector = PersonId.Real("fixture_staff_td");
    private static readonly PersonId Scout = PersonId.Real("fixture_staff_scout");
    private static readonly PersonId Mechanic = PersonId.Real("fixture_staff_mechanic");
    private static readonly PersonId PrincipalB = PersonId.Real("fixture_staff_principal_b");
    private static readonly GameDate YearEnd = new(1955, 12, 31);

    private static Lab Team()
    {
        return new Lab(customize: world =>
        {
            foreach (var (id, role, family) in new[]
            {
                (TechDirector, StaffRole.TechnicalDirector, "Director"),
                (Scout, StaffRole.Scout, "Scout"),
                (Mechanic, StaffRole.ChiefMechanic, "Mechanic"),
                (PrincipalB, StaffRole.TeamPrincipal, "Boss"),
            })
            {
                (world, _) = world.AddPerson(new PersonSpec("Sam", family, new GameDate(1920, 5, 5), "GBR", true, id.Value, [PersonRole.Staff(role)], StaffTruth(role, 12)));
                (world, _) = world.AddContract(new ContractSpec(id, TeamB, ContractRole.Staff(role), new GameDate(1955, 1, 1), YearEnd, role == StaffRole.TeamPrincipal ? 0 : 150_000, true, null, null));
                foreach (var team in new[] { TeamA, TeamB, TeamC })
                {
                    var truth = StaffTruth(role, 12);
                    world = world.SetKnowledge(new PersonKnowledge(
                        team,
                        id,
                        truth.Attributes.Select(attribute => new KnownAttribute(attribute.Key, new AttributeBand(attribute.Value - 1, attribute.Value + 1))).ToArray(),
                        null));
                }
            }

            return world;
        });
    }

    /// <summary>One morning and one day the way the host does it: lapsed items are expired as commands, both humans press ready, the gate decides.</summary>
    private static AdvanceResult LiveOneDay(Lab lab)
    {
        InboxExpiry.EnqueueDue(lab.Queue, lab.Inbox, lab.Today);
        lab.Dispatcher.DispatchAll(lab.Queue, lab.Context);
        lab.Managers.SetReady(Anna, true);
        lab.Managers.SetReady(Bram, true);
        return lab.Gate.RequestAdvance(lab.Managers, lab.Time);
    }

    private static bool HasContractOn(Lab lab, PersonId person, OrganizationId team, GameDate day) =>
        lab.World.Contracts.Any(contract => contract.PersonId == person && contract.OrganizationId == team && contract.IsActiveOn(day));

    [Fact]
    public void ASeasonWithNoAnswerToAnyRenewalReachesNewYearWithTheDefaultsApplied()
    {
        var lab = Team();

        while (lab.Today < new GameDate(1956, 1, 1))
        {
            Assert.IsType<AdvanceResult.Advanced>(LiveOneDay(lab));
        }

        var newYear = new GameDate(1956, 1, 1);
        foreach (var person in new[] { Veteran, TechDirector, Scout, Mechanic, PrincipalB })
        {
            Assert.True(HasContractOn(lab, person, TeamB, newYear), person.Value + " lost the contract. " + string.Join(" | ", lab.Inbox.Section.ItemsOf(Bram.Value).Select(i => i.Kind + ":" + i.Status + ":" + i.ChosenOptionId + ":" + i.SubjectKey)));
        }

        Assert.DoesNotContain(lab.Inbox.Section.Items, item => item.IsOpenDecision);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
    }

    [Fact]
    public void NoRenewalPromptHoldsTheClockOnTheDayItIsSent()
    {
        var lab = Team();
        lab.Advance(31);

        var open = lab.Inbox.Section.ItemsOf(Bram.Value).Where(item => item.IsOpenDecision).ToArray();

        Assert.NotEmpty(open);
        Assert.All(open, item => Assert.False(item.IsHoldingClock));
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
        Assert.IsType<AdvanceResult.Advanced>(LiveOneDay(lab));
    }

    [Fact]
    public void DriversAndKeyStaffGetTheirOwnPromptAndOtherStaffOneGroupedItem()
    {
        var lab = Team();
        lab.Advance(31);

        var items = lab.Inbox.Section.ItemsOf(Bram.Value).Where(item => item.IsOpenDecision).ToArray();

        var single = items.Where(item => item.Kind == ContractEngine.RenewalKind).Select(item => item.Arguments["person"]).Order().ToArray();
        Assert.Equal(2, single.Length);
        Assert.Contains(single, name => name.Contains("Veteran", StringComparison.Ordinal));
        Assert.Contains(single, name => name.Contains("Director", StringComparison.Ordinal));
        var group = Assert.Single(items, item => item.Kind == ContractEngine.RenewalGroupKind);
        Assert.Equal("2", group.Arguments["count"]);
        Assert.Equal(ContractEngine.OptionExtend, group.DefaultOptionId);
        Assert.All(items, item =>
        {
            Assert.Equal(item.Created.AddDays(NegotiationEstimates.RenewalDecisionDays), item.ValidUntil);
            Assert.Equal(ContractEngine.OptionExtend, item.DefaultOptionId);
        });
    }

    [Fact]
    public void ThePrincipalsOwnContractIsExtendedByTheBoardWithNoPromptToRelease()
    {
        var lab = Team();
        lab.Advance(31);

        Assert.DoesNotContain(
            lab.Inbox.Section.ItemsOf(Bram.Value),
            item => item.Kind != ContractEngine.ContractNoticeKind && item.Arguments.TryGetValue("person", out var name) && name.Contains("Boss", StringComparison.Ordinal));
        Assert.True(HasContractOn(lab, PrincipalB, TeamB, new GameDate(1956, 6, 1)));
        Assert.Contains(lab.Inbox.Section.ItemsOf(Bram.Value), item => item.SubjectKey == ContractKeys.NoticePrincipalExtended);
    }

    [Fact]
    public void AnswerRelease_OverridesTheDefaultForTheGroupAndThoseContractsRunOut()
    {
        var lab = Team();
        lab.Advance(31);
        var group = lab.Inbox.Section.Items.Single(item => item.Kind == ContractEngine.RenewalGroupKind);

        var result = lab.Submit(new ResolveInboxItemCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), ItemId = group.Id, OptionId = ContractEngine.OptionRelease });
        Assert.IsType<CommandResult.Accepted>(result);
        while (lab.Today < new GameDate(1956, 1, 1))
        {
            Assert.IsType<AdvanceResult.Advanced>(LiveOneDay(lab));
        }

        Assert.False(HasContractOn(lab, Scout, TeamB, lab.Today));
        Assert.False(HasContractOn(lab, Mechanic, TeamB, lab.Today));
        Assert.True(HasContractOn(lab, Veteran, TeamB, lab.Today));
    }

    [Fact]
    public void WhenTheBudgetCannotCarryTheDefaultTheContractRunsOutAndTheManagerIsTold()
    {
        var lab = Team();
        lab.Payroll.Cap(TeamB, 10_000);

        while (lab.Today < new GameDate(1956, 1, 1))
        {
            Assert.IsType<AdvanceResult.Advanced>(LiveOneDay(lab));
        }

        Assert.False(HasContractOn(lab, Veteran, TeamB, lab.Today));
        var notices = lab.Inbox.Section.ItemsOf(Bram.Value).Where(item => item.SubjectKey == ContractKeys.NoticeExtendFailed).ToArray();
        Assert.Contains(notices, item => item.Arguments["person"].Contains("Veteran", StringComparison.Ordinal));
        Assert.Contains(notices, item => item.Arguments["person"].Contains("Director", StringComparison.Ordinal));
        Assert.Equal(2, notices.Count(item => item.Arguments["person"].Contains("Scout", StringComparison.Ordinal) || item.Arguments["person"].Contains("Mechanic", StringComparison.Ordinal)));
    }

    [Fact]
    public void ExtendingFromThePromptDoesNotNeedANegotiationSlot()
    {
        var lab = Team();
        lab.Advance(31);
        var prompt = lab.Inbox.Section.Items.First(item => item.Kind == ContractEngine.RenewalKind);

        var result = lab.Submit(new ResolveInboxItemCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), ItemId = prompt.Id, OptionId = ContractEngine.OptionExtend });

        var events = Assert.IsType<CommandResult.Accepted>(result).Events;
        Assert.Contains(events, e => e is ContractExtended);
        Assert.Empty(lab.Section.Negotiations);
    }
}
