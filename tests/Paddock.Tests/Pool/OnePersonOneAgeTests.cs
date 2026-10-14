using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Domain.Pool;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Contracts;

namespace Paddock.Tests.Pool;

/// <summary>
/// One person has one age on one date (#326). The academy (the pool), the market and the driver profile are three screens that show
/// the same person, so they must agree on the age, on 1 January and on a birthday. Synthetic person, no real data.
/// </summary>
public sealed class OnePersonOneAgeTests
{
    // Born on 15 June 1938: 17 on 1 January 1956, who has not had his 18th birthday yet.
    private static readonly GameDate Born = new(1938, 6, 15);

    public static TheoryData<int, int, int, int> Days => new()
    {
        { 1956, 1, 1, 17 },
        { 1956, 6, 14, 17 },
        { 1956, 6, 15, 18 },
        { 1956, 12, 31, 18 },
    };

    [Theory]
    [MemberData(nameof(Days))]
    public void TheAcademyTheMarketAndTheProfileShowTheSameAge(int year, int month, int day, int expected)
    {
        var today = new GameDate(year, month, day);
        var lab = new ContractKit.Lab(customize: world =>
        {
            var (added, id) = world.AddPerson(new PersonSpec("Tuero", "Test", Born, "ARG", false, null, [PersonRole.Driver], PoolKit.Truth(10, 4)));
            return added.WithDate(today).WithSection(TalentPoolSection.Empty.EnterAll([id], today));
        });
        var person = lab.World.Persons.Single(item => item.FamilyName == "Test");

        var pool = new PoolQuery(new PoolBook(() => lab.World, _ => { }), new NoTeams()).View(AccessContext.Developer);
        var market = MarketRead.Of(AccessContext.ForManager(new ManagerId(ContractKit.Anna.Value)), lab.Book, ContractKit.TeamA, today);
        var profile = DriverProfileRead.Of(lab.World, ContractKit.TeamA, today, person.Id.Value);

        Assert.Equal(expected, pool.Items.Single().Age);
        Assert.Equal(expected, market.FreeAgents.Single(item => item.PersonId == person.Id.Value).Age);
        Assert.Equal(expected, profile.Age);
    }

    private sealed class NoTeams : IManagerOrganizations
    {
        public OrganizationId? OrganizationOf(string managerId) => null;
    }
}
