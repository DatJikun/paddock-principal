using System.Globalization;
using System.Text.Json;
using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Desktop.Bridge;
using Paddock.Domain.Board;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Tests.Desktop;
using static Paddock.Tests.Board.BoardKit;

namespace Paddock.Tests.Board;

/// <summary>
/// #315: the three board targets sit close together for a strong team, the gap grows with the expectation and the field, a harder
/// target pays a larger bonus that the player sees before choosing, and the outcome arrives in the inbox with its reason.
/// </summary>
public class SeasonTargetSpreadTests
{
    [Fact]
    public void Ferrari2017NoLongerGetsP1P3P5()
    {
        // The owner's playtest: Ferrari expected third of ten teams got P1 / P3 / P5. One place either side now.
        Assert.Equal(2, SeasonTarget.Position(3, 10, SeasonAmbition.Ambitious));
        Assert.Equal(3, SeasonTarget.Position(3, 10, SeasonAmbition.Expected));
        Assert.Equal(4, SeasonTarget.Position(3, 10, SeasonAmbition.Safe));

        // A team expected second gets the owner's example, P1 / P2 / P3.
        Assert.Equal(1, SeasonTarget.Position(2, 10, SeasonAmbition.Ambitious));
        Assert.Equal(2, SeasonTarget.Position(2, 10, SeasonAmbition.Expected));
        Assert.Equal(3, SeasonTarget.Position(2, 10, SeasonAmbition.Safe));
    }

    [Fact]
    public void EveryExpectationInEveryFieldGetsThreeDistinctTargets_AndATopFourTeamOnePlaceEitherSide()
    {
        for (var field = 2; field <= 34; field++)
        {
            for (var expected = 1; expected <= field; expected++)
            {
                var safe = SeasonTarget.Position(expected, field, SeasonAmbition.Safe);
                var middle = SeasonTarget.Position(expected, field, SeasonAmbition.Expected);
                var ambitious = SeasonTarget.Position(expected, field, SeasonAmbition.Ambitious);
                var wins = SeasonTarget.WinsRequired(expected, field, 16, SeasonAmbition.Ambitious);
                var label = $"expected {expected} of {field}: P{ambitious} / P{middle} / P{safe}";
                Assert.True(safe > middle, label);
                Assert.True(middle > ambitious || (middle == ambitious && wins > 0), label);
                Assert.InRange(safe, 1, field);
                Assert.InRange(ambitious, 1, field);
                Assert.True(safe - middle <= BoardEstimates.TargetMaxPlaces, label);
                if (expected <= 4)
                {
                    Assert.True(safe - middle == 1 && middle - ambitious <= 1, label);
                }
            }
        }
    }

    [Fact]
    public void TheGapGrowsTowardsTheBackAndWithTheField()
    {
        for (var field = 3; field <= 34; field++)
        {
            for (var expected = 2; expected <= field; expected++)
            {
                Assert.True(SeasonTarget.Places(expected, field) >= SeasonTarget.Places(expected - 1, field), $"{expected} of {field}");
            }
        }

        // The last team of a big field has more room than the last team of a small one.
        Assert.Equal(2, SeasonTarget.Places(10, 10));
        Assert.Equal(3, SeasonTarget.Places(20, 20));
        Assert.True(SeasonTarget.Places(10, 20) <= SeasonTarget.Places(20, 20));
    }

    [Fact]
    public void AHarderTargetPaysMore_SoTheSafeOneIsNotBetterInEveryNumber()
    {
        const long typical = 150_000_000;
        var safe = SeasonTarget.BonusDollars(SeasonAmbition.Safe, typical);
        var expected = SeasonTarget.BonusDollars(SeasonAmbition.Expected, typical);
        var ambitious = SeasonTarget.BonusDollars(SeasonAmbition.Ambitious, typical);
        Assert.True(safe > 0);
        Assert.True(safe < expected && expected < ambitious);
        Assert.True(SeasonTarget.RewardTenths(SeasonAmbition.Safe) < SeasonTarget.RewardTenths(SeasonAmbition.Ambitious));
        Assert.Equal(0, SeasonTarget.BonusDollars(SeasonAmbition.Ambitious, 0));
    }

    [Theory]
    [InlineData(1958)]
    [InlineData(1975)]
    [InlineData(1996)]
    [InlineData(2010)]
    [InlineData(2017)]
    public void EveryTeamOfAStartYearIsOfferedThreeDistinctTargets(int year)
    {
        var lobby = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var teams = lobby.Handle(Message("teams", "query", "teams", "{\"managerId\":\"human:player\",\"year\":" + year + "}"));
        using var list = JsonDocument.Parse(teams.Response);
        var ids = list.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray()
            .Select(team => team.GetProperty("id").GetString()!)
            .ToArray();
        Assert.NotEmpty(ids);
        foreach (var team in ids)
        {
            var (safe, expected, ambitious, wins) = TargetsOf(team, year);
            var label = $"{team} {year}: P{ambitious} / P{expected} / P{safe}";
            Assert.True(safe > expected, label);
            Assert.True(expected > ambitious || (expected == ambitious && wins > 0), label);
            if (expected <= 4)
            {
                Assert.True(safe - ambitious <= 2, label);
            }
        }
    }

    [Fact]
    public void TheDecisionShowsTheBonusOfEachTarget_AndAMetTargetPaysTheShownBonus()
    {
        var bonus = new FakeBoardBonus(1_000_000);
        var lab = new Lab(noRaces: true, bonus: bonus);
        lab.Appoint(Pam, T3);
        lab.Advance(1);

        var decision = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen);
        Assert.Equal("10000", decision.Arguments["safeBonus"]);
        Assert.Equal("30000", decision.Arguments["expectedBonus"]);
        Assert.Equal("80000", decision.Arguments["ambitiousBonus"]);
        Choose(lab, decision, SeasonTarget.Expected);
        var season = lab.Board.Objectives.OwnedBy(T3).Single(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(BoardKeys.EffectConfidenceUpBonus, season.EffectOnMet.Key);
        Assert.Equal("30000", season.EffectOnMet.Arguments["bonus"]);

        lab.Facts.Position(T3, 2);
        lab.AdvanceTo(new GameDate(1955, 12, 31));
        lab.Advance(1);

        var paid = Assert.Single(bonus.Paid);
        Assert.Equal((T3, 30_000L, new GameDate(1955, 12, 31)), paid);
        var notice = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.SubjectKey == BoardKeys.NoticeTargetMetBonus);
        Assert.Equal("3", notice.Arguments["target"]);
        Assert.Equal("2", notice.Arguments["finish"]);
        Assert.Equal("30000", notice.Arguments["bonus"]);
        Assert.Equal("15", notice.Arguments["points"]);
    }

    [Fact]
    public void AMissedTargetPaysNothingAndTellsThePlayerWhy()
    {
        var bonus = new FakeBoardBonus(1_000_000);
        var lab = new Lab(noRaces: true, bonus: bonus);
        lab.Appoint(Pam, T3);
        lab.Advance(1);
        Choose(lab, Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen), SeasonTarget.Ambitious);
        var before = lab.Section.Board(T3)!.ConfidenceTenths;

        lab.Facts.Position(T3, 4);
        lab.AdvanceTo(new GameDate(1955, 12, 31));
        lab.Advance(1);

        Assert.Empty(bonus.Paid);
        Assert.Equal(before - BoardEstimates.AmbitiousFailTenths, lab.Section.Board(T3)!.ConfidenceTenths);

        // The new principal is still protected, so the notice says only the protection kept the job.
        var notice = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.NoticeKind);
        Assert.Equal(BoardKeys.NoticeTargetFailedSpared, notice.SubjectKey);
        Assert.Equal("2", notice.Arguments["target"]);
        Assert.Equal("4", notice.Arguments["finish"]);
        Assert.Equal("40", notice.Arguments["points"]);
        Assert.Equal(Pam.Value, lab.Section.Board(T3)!.Principal!.Subject);
    }

    [Fact]
    public void AnAiTeamIsPaidByTheSameRules()
    {
        var bonus = new FakeBoardBonus(1_000_000);
        var lab = new Lab(noRaces: true, bonus: bonus);
        lab.Facts.Position(T1, 1);
        lab.AdvanceTo(new GameDate(1955, 12, 31));
        lab.Advance(1);

        Assert.Contains(bonus.Paid, payment => payment.Organization == T1 && payment.Amount == 30_000L);
    }

    private static void Choose(Lab lab, InboxItem decision, string option) =>
        Assert.IsType<CommandResult.Accepted>(lab.Submit(new ResolveInboxItemCommand
        {
            ManagerId = Pam,
            IssuedOn = Date(lab.Today),
            ItemId = decision.Id,
            OptionId = option,
        }));

    private static (int Safe, int Expected, int Ambitious, int Wins) TargetsOf(string team, int year)
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var started = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            "{\"managerId\":\"human:player\",\"teamId\":\"" + team + "\",\"year\":" + year
            + ",\"givenName\":\"Enzo\",\"familyName\":\"Test\",\"nationality\":\"IT\",\"tilt\":\"none\",\"preset\":\"Balanced\"}"));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var inbox = host.Handle(Message("in", "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        var item = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind);
        var parameters = item.GetProperty("subject").GetProperty("parameters");
        int Read(string name) => int.Parse(parameters.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);
        return (Read("safeTarget"), Read("expectedTarget"), Read("ambitiousTarget"), Read("ambitiousWins"));
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
