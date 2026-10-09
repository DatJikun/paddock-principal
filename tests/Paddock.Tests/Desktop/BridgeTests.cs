using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Paddock.Application.Board;
using Paddock.Desktop.Bridge;
using Paddock.Domain.Board;

namespace Paddock.Tests.Desktop;

public class BridgeTests
{
    [Fact]
    public void QueryRoundTripReturnsTheOpeningDate()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("q1", "query", "shell"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        Assert.Equal(CareerBridge.HumanManagerId, json.RootElement.GetProperty("data").GetProperty("managerId").GetString());
        Assert.Empty(exchange.Events);
    }

    [Fact]
    public void TheStartScreenOffersTheYear2010()
    {
        // #323: the owner's playtest year is the one the wizard opens on.
        using var lobby = Lobby();
        var session = lobby.Host.Handle(Message("session", "query", "session"));
        using var json = JsonDocument.Parse(session.Response);
        Assert.Equal(2010, json.RootElement.GetProperty("data").GetProperty("suggestedYear").GetInt32());
    }

    [Fact]
    public void ANewCareerThatNamesNoYearStartsIn2010()
    {
        // #323: a career started with the defaults (no year sent) opens in 2010, not in the pinned 1955 world.
        using var lobby = Lobby();
        var started = lobby.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Balanced"}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var shell = lobby.Host.Handle(Message("sh", "query", "shell"));
        using var shellJson = JsonDocument.Parse(shell.Response);
        Assert.Equal("2010-01-01", shellJson.RootElement.GetProperty("data").GetProperty("date").GetString());
    }

    [Fact]
    public void EveryRegisteredQueryReturnsOk()
    {
        using var career = Open();
        foreach (var endpoint in BridgeRegistry.Endpoints)
        {
            if (endpoint.Kind != BridgeRegistry.Query)
            {
                continue;
            }

            var exchange = career.Host.Handle(Message("q-" + endpoint.Name, "query", endpoint.Name));
            using var json = JsonDocument.Parse(exchange.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), endpoint.Name + " " + exchange.Response);
        }
    }

    [Fact]
    public void UnknownNameReturnsItsReasonKey()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("u1", "query", "notAQuery"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeKeys.UnknownName, json.RootElement.GetProperty("error").GetProperty("key").GetString());
        Assert.Equal("notAQuery", json.RootElement.GetProperty("error").GetProperty("parameters").GetProperty("name").GetString());
    }

    [Fact]
    public void RejectedCommandReturnsTheReasonKeyAndDoesNotChangeTheWorld()
    {
        using var career = Open();
        var before = career.Host.StateHash;
        var exchange = career.Host.Handle(Message(
            "c1",
            "command",
            "resolveInbox",
            """{"managerId":"human:player","itemId":"missing","optionId":"yes"}"""));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("key").GetString()));
        Assert.Equal(before, career.Host.StateHash);
        Assert.Empty(exchange.Events);
    }

    [Fact]
    public void OpeningOffersTheSeasonTargetWithItsReason()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("in", "query", "inbox"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        var match = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind);
        Assert.Equal(BoardKeys.SeasonTargetSubject, match.GetProperty("subject").GetProperty("key").GetString());
        Assert.Equal(SeasonTarget.Expected, match.GetProperty("defaultOptionId").GetString());
    }

    [Fact]
    public void TheShellNamesTheDecisionWithItsParameters()
    {
        // Regression: the top bar got only the subject key, so "P{safeTarget}" reached the screen unfilled.
        using var career = Open();
        var exchange = career.Host.Handle(Message("sh", "query", "shell"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(BoardEngine.SeasonTargetKind, data.GetProperty("decisionKind").GetString());
        var subject = data.GetProperty("decisionSubject");
        Assert.Equal(BoardKeys.SeasonTargetSubject, subject.GetProperty("key").GetString());
        Assert.False(string.IsNullOrWhiteSpace(subject.GetProperty("parameters").GetProperty("safeTarget").GetString()));
    }

    [Fact]
    public void TheTrackQueryReadsALayoutWithoutChangingTheWorld()
    {
        using var career = Open();
        var before = career.Host.StateHash;
        var exchange = career.Host.Handle(Message(
            "tr",
            "query",
            "track",
            "{\"managerId\":\"human:player\",\"layoutId\":\"galvez_1953\"}"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("found").GetBoolean(), exchange.Response);
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("name").GetString()));
        Assert.True(data.GetProperty("lengthKm").GetDouble() > 0);
        Assert.True(data.GetProperty("points").GetArrayLength() >= 3);
        Assert.Equal(0, data.GetProperty("races").GetInt32());
        Assert.Equal(before, career.Host.StateHash);

        var named = career.Host.Handle(Message(
            "tr2",
            "query",
            "track",
            "{\"managerId\":\"human:player\",\"layoutId\":\"monza_1955\"}"));
        using var namedJson = JsonDocument.Parse(named.Response);
        Assert.Equal("monza", namedJson.RootElement.GetProperty("data").GetProperty("circuitId").GetString());

        var missing = career.Host.Handle(Message(
            "tr3",
            "query",
            "track",
            "{\"managerId\":\"human:player\",\"layoutId\":\"nowhere_1999\"}"));
        using var missingJson = JsonDocument.Parse(missing.Response);
        Assert.False(missingJson.RootElement.GetProperty("data").GetProperty("found").GetBoolean());
    }

    [Fact]
    public void TheAiPrincipalDirectorLeavesThePlayersTeamAlone()
    {
        // Regression: without seating the player, the T44 director ran the player's team as an AI team
        // (for example it set that team's scouting focus on the first morning).
        using var career = Open();
        AcceptExpectedSeasonTarget(career.Host, "target");
        for (var day = 0; day < 30; day++)
        {
            var pool = career.Host.Handle(Message("p" + day, "query", "pool"));
            using var poolJson = JsonDocument.Parse(pool.Response);
            Assert.True(poolJson.RootElement.GetProperty("ok").GetBoolean(), poolJson.RootElement.ToString());
            Assert.Equal(JsonValueKind.Null, poolJson.RootElement.GetProperty("data").GetProperty("focus").ValueKind);

            var moved = career.Host.Handle(Message("a" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            if (!movedJson.RootElement.GetProperty("ok").GetBoolean())
            {
                break;
            }
        }
    }

    [Fact]
    public void AdvanceDayMovesTheDateAndASecondCareerAgrees()
    {
        using var first = Open();
        using var second = Open();
        AcceptExpectedSeasonTarget(first.Host, "t1");
        AcceptExpectedSeasonTarget(second.Host, "t2");
        var moved = first.Host.Handle(Message("a1", "command", "advanceDay"));
        using var json = JsonDocument.Parse(moved.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), json.RootElement.ToString());
        Assert.Equal("1955-01-02", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        Assert.Contains(moved.Events, item => item.Contains("dayAdvanced", StringComparison.Ordinal));

        second.Host.Handle(Message("a1", "command", "advanceDay"));
        Assert.Equal(first.Host.StateHash, second.Host.StateHash);

        var shell = first.Host.Handle(Message("q2", "query", "shell"));
        using var shellJson = JsonDocument.Parse(shell.Response);
        Assert.Equal("1955-01-02", shellJson.RootElement.GetProperty("data").GetProperty("date").GetString());
    }

    [Fact]
    public void AFerrariCareerReachesTheFirstRaceAndASaveResumesTheSameWorld()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var json = JsonDocument.Parse(started.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
            Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
            Assert.Equal("ferrari", json.RootElement.GetProperty("data").GetProperty("organizationId").GetString());
        }

        var refused = career.Host.Handle(Message(
            "split",
            "command",
            "setDevelopmentSplit",
            """{"managerId":"human:player","organizationId":"mercedes","nextPercent":30}"""));
        using (var json = JsonDocument.Parse(refused.Response))
        {
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), refused.Response);
            Assert.Equal("development.error.notInControl", json.RootElement.GetProperty("error").GetProperty("key").GetString());
        }

        var raced = false;
        for (var day = 0; day < 90 && !raced; day++)
        {
            var pool = career.Host.Handle(Message("pool" + day, "query", "pool"));
            using (var poolJson = JsonDocument.Parse(pool.Response))
            {
                Assert.Equal(JsonValueKind.Null, poolJson.RootElement.GetProperty("data").GetProperty("focus").ValueKind);
            }

            ClearDecision(career.Host, day);
            var moved = career.Host.Handle(Message("day" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            Assert.True(movedJson.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
            var table = career.Host.Handle(Message("table" + day, "query", "standings"));
            using var tableJson = JsonDocument.Parse(table.Response);
            Assert.True(tableJson.RootElement.GetProperty("ok").GetBoolean(), table.Response);
            if (tableJson.RootElement.GetProperty("data").GetProperty("roundsCompleted").GetInt32() >= 1)
            {
                raced = true;
            }
        }

        Assert.True(raced, "the first 1955 race did not finish");
        var standings = career.Host.Handle(Message("standings", "query", "standings"));
        using (var json = JsonDocument.Parse(standings.Response))
        {
            var data = json.RootElement.GetProperty("data");
            var leader = data.GetProperty("drivers")[0];
            Assert.False(string.IsNullOrWhiteSpace(leader.GetProperty("nationality").GetString()), standings.Response);
            Assert.False(string.IsNullOrWhiteSpace(leader.GetProperty("teamName").GetString()), standings.Response);
            Assert.True(data.GetProperty("rules").GetProperty("countedResults").GetInt32() >= 0);
        }

        var track = career.Host.Handle(Message("track", "query", "track", "{\"managerId\":\"human:player\",\"layoutId\":\"galvez_1953\"}"));
        using (var json = JsonDocument.Parse(track.Response))
        {
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(1, data.GetProperty("races").GetInt32());
            Assert.Single(data.GetProperty("winners").EnumerateArray());
            var past = Assert.Single(data.GetProperty("past").EnumerateArray());
            Assert.Equal("career", past.GetProperty("source").GetString());
            Assert.Equal(1955, past.GetProperty("season").GetInt32());
        }

        var overview = career.Host.Handle(Message("overview", "query", "seasonOverview"));
        using (var json = JsonDocument.Parse(overview.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), overview.Response);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.GetProperty("rounds").GetArrayLength() > 1);
            var rows = data.GetProperty("drivers");
            Assert.True(rows.GetArrayLength() > 1);
            Assert.Equal(data.GetProperty("rounds").GetArrayLength(), rows[0].GetProperty("cells").GetArrayLength());
            Assert.DoesNotContain("spy", overview.Response, StringComparison.OrdinalIgnoreCase);
        }
        var calendar = career.Host.Handle(Message("calendar", "query", "calendar"));
        using (var json = JsonDocument.Parse(calendar.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), calendar.Response);
            var rounds = json.RootElement.GetProperty("data").GetProperty("rounds");
            Assert.True(rounds.GetArrayLength() > 1);
            Assert.Contains(rounds.EnumerateArray(), round => round.GetProperty("finished").GetBoolean());
        }

        AssertCalendarKeepsFinishedRaceDate(career.Host);
        AssertPlayerRaceResultHasNoSpy(career.Host);

        var next = career.Host.Handle(Message("next", "query", "nextRace"));
        using (var json = JsonDocument.Parse(next.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), next.Response);
        }

        var result = career.Host.Handle(Message("result", "query", "raceResult"));
        using (var json = JsonDocument.Parse(result.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), result.Response);
            Assert.True(json.RootElement.GetProperty("data").GetProperty("found").GetBoolean());
            Assert.True(json.RootElement.GetProperty("data").GetProperty("rows").GetArrayLength() > 1);
            Assert.True(json.RootElement.GetProperty("data").GetProperty("sections").GetArrayLength() > 0);
            var facts = json.RootElement.GetProperty("data").GetProperty("facts");
            Assert.True(facts.GetProperty("laps").GetInt32() > 0);
            Assert.True(facts.GetProperty("distanceMeters").GetInt32() > 0);
            Assert.False(string.IsNullOrEmpty(facts.GetProperty("pole").GetProperty("driverName").GetString()));
            Assert.False(string.IsNullOrEmpty(facts.GetProperty("fastestLap").GetProperty("driverName").GetString()));
            var first = json.RootElement.GetProperty("data").GetProperty("rows")[0];
            Assert.True(first.GetProperty("gridPosition").GetInt32() >= 1);
            Assert.True(first.GetProperty("timeMs").GetInt64() > 0);
        }

        var hash = career.Host.StateHash;
        var name = "bridge-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeTestData.SavesDirectory, name + ".paddock");
        try
        {
            var saved = career.Host.Handle(Message(
                "save",
                "command",
                "saveCareer",
                "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}"));
            using (var json = JsonDocument.Parse(saved.Response))
            {
                Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), saved.Response);
                Assert.Equal(hash, json.RootElement.GetProperty("data").GetProperty("hash").GetString());
            }

            using var loaded = Lobby();
            var back = loaded.Host.Handle(Message(
                "load",
                "command",
                "loadCareer",
                "{\"managerId\":\"human:player\",\"path\":\"" + name + "\"}"));
            using var loadedJson = JsonDocument.Parse(back.Response);
            Assert.True(loadedJson.RootElement.GetProperty("ok").GetBoolean(), back.Response);
            Assert.Equal(hash, loaded.Host.StateHash);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AfterRoundOneTheCalendarStillReturnsThatRoundsRaceDate()
    {
        using var career = StartFerrariCareer();
        AdvanceUntilFirstRace(career.Host);
        AssertCalendarKeepsFinishedRaceDate(career.Host);
    }

    [Fact]
    public void RaceResultForTheHumanPlayerHasNoSpySection()
    {
        using var career = StartFerrariCareer();
        AdvanceUntilFirstRace(career.Host);
        AssertPlayerRaceResultHasNoSpy(career.Host);
    }

    [Theory]
    [InlineData("mercedes", 1)]
    [InlineData("ferrari", 2)]
    [InlineData("lancia", 9)]
    [InlineData("arzani-volpini", 10)]
    public void TheTeamCardsExpectWhatTheBoardOffersOnDayOne(string team, int expected)
    {
        // #234: the card on the team list and the season target of the first morning read one rule (the budget level of the team,
        // then the authored 1954 order), so a player is not promised one place and asked for another.
        using var lobby = Lobby();
        var cards = lobby.Host.Handle(Message(
            "cards",
            "query",
            "teams",
            """{"managerId":"human:player","year":1955,"preset":"Chaos","people":null,"seed":1}"""));
        using var cardsJson = JsonDocument.Parse(cards.Response);
        var card = cardsJson.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == team);
        Assert.Equal(expected, card.GetProperty("expected").GetInt32());

        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            "{\"managerId\":\"human:player\",\"teamId\":\"" + team + "\",\"givenName\":\"Enzo\",\"familyName\":\"Test\",\"nationality\":\"IT\",\"tilt\":\"none\",\"preset\":\"Chaos\",\"year\":1955,\"seed\":1}"));
        using var startedJson = JsonDocument.Parse(started.Response);
        Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        var shell = career.Host.Handle(Message("sh", "query", "shell"));
        using var shellJson = JsonDocument.Parse(shell.Response);
        var offered = shellJson.RootElement.GetProperty("data").GetProperty("decisionSubject").GetProperty("parameters");
        Assert.Equal(card.GetProperty("expected").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture), offered.GetProperty("expected").GetString());
        Assert.Equal(card.GetProperty("fieldSize").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture), offered.GetProperty("field").GetString());
    }

    [Fact]
    public void TheTeamCardsComeInOrderOfLastSeasonWithABudgetAndRoughLevels()
    {
        // #266: the cards are listed by last season's finish (teams without a known place last), carry the opening budget as money,
        // and show four coarse levels from 1 to 5 that follow the paddock's picture of a team, not a hidden score.
        using var lobby = Lobby();
        var response = lobby.Host.Handle(Message(
            "cards",
            "query",
            "teams",
            """{"managerId":"human:player","year":1955,"preset":"Chaos","people":null,"seed":1}"""));
        using var json = JsonDocument.Parse(response.Response);
        var teams = json.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray().ToArray();
        var places = teams
            .Select(team => team.GetProperty("lastSeason").ValueKind == JsonValueKind.Null ? int.MaxValue : team.GetProperty("lastSeason").GetInt32())
            .ToArray();
        Assert.Equal(places.OrderBy(place => place).ToArray(), places);
        Assert.Contains(places, place => place != int.MaxValue);

        foreach (var team in teams)
        {
            Assert.True(team.GetProperty("budgetCents").GetInt64() > 0, team.GetRawText());
            foreach (var area in new[] { "car", "infrastructure", "drivers", "staff" })
            {
                var level = team.GetProperty("levels").GetProperty(area);
                if (level.ValueKind != JsonValueKind.Null)
                {
                    Assert.InRange(level.GetInt32(), 1, 5);
                }
            }
        }

        JsonElement Card(string id) => teams.Single(team => team.GetProperty("id").GetString() == id);
        Assert.True(Card("mercedes").GetProperty("levels").GetProperty("car").GetInt32() > Card("cooper").GetProperty("levels").GetProperty("car").GetInt32());
        Assert.True(Card("mercedes").GetProperty("levels").GetProperty("infrastructure").GetInt32() > Card("cooper").GetProperty("levels").GetProperty("infrastructure").GetInt32());
    }

    [Fact]
    public void TheShellNamesTheNewestOpenImportantItemSoAutomaticPlayCanStopOnIt()
    {
        using var career = StartFerrariCareer();
        var shell = career.Host.Handle(Message("sh", "query", "shell"));
        using var shellJson = JsonDocument.Parse(shell.Response);
        var data = shellJson.RootElement.GetProperty("data");
        // The board's season target asks for an answer, so it is important and holds the clock.
        Assert.Equal(data.GetProperty("decisionItemId").GetString(), data.GetProperty("importantItemId").GetString());
        Assert.Equal("board.seasonTarget", data.GetProperty("importantKind").GetString());
        Assert.Equal(JsonValueKind.Object, data.GetProperty("importantSubject").ValueKind);

        var inbox = career.Host.Handle(Message("in", "query", "inbox"));
        using var inboxJson = JsonDocument.Parse(inbox.Response);
        foreach (var item in inboxJson.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            // Every item that asks for an answer is important.
            if (item.GetProperty("needsDecision").GetBoolean())
            {
                Assert.True(item.GetProperty("important").GetBoolean(), item.GetRawText());
            }
        }
    }

    [Theory]
    [InlineData("board.seasonTarget", true, true)]
    [InlineData("contract.notice", false, true)]
    [InlineData("infrastructure.buildFinished", false, true)]
    [InlineData("sponsor.notice", false, false)]
    [InlineData("negotiation.notice", false, false)]
    public void OnlyDecisionsAndNoticesThatChangeTheSituationAreImportant(string kind, bool needsDecision, bool important)
    {
        Assert.Equal(important, Paddock.Application.Inbox.InboxImportance.IsImportant(kind, needsDecision));
    }

    [Fact]
    public void TheTeamCardsShowTheLineUpTheCareerWillStartWithAndChangeNothing()
    {
        using var career = Lobby();
        var args = """{"managerId":"human:player","year":1955,"preset":"Chaos","people":null,"seed":1}""";
        var first = career.Host.Handle(Message("cards", "query", "teams", args));
        var again = career.Host.Handle(Message("cards2", "query", "teams", args));
        using var json = JsonDocument.Parse(first.Response);
        using var twice = JsonDocument.Parse(again.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), first.Response);
        Assert.Equal(
            json.RootElement.GetProperty("data").GetRawText(),
            twice.RootElement.GetProperty("data").GetRawText());
        Assert.Empty(first.Events);

        JsonElement? ferrari = null;
        foreach (var team in json.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray())
        {
            Assert.True(team.GetProperty("expected").GetInt32() >= 1, team.GetRawText());
            Assert.True(team.GetProperty("expected").GetInt32() <= team.GetProperty("fieldSize").GetInt32(), team.GetRawText());
            Assert.Contains(team.GetProperty("budget").GetString(), new[] { "low", "typical", "top" });
            if (team.GetProperty("id").GetString() == "ferrari")
            {
                ferrari = team.Clone();
            }
        }

        Assert.NotNull(ferrari);
        var card = ferrari.Value;
        Assert.NotEmpty(card.GetProperty("drivers").EnumerateArray());
        Assert.Equal("works", card.GetProperty("engine").GetProperty("supplyType").GetString());

        var session = career.Host.Handle(Message("session", "query", "session"));
        using (var sessionJson = JsonDocument.Parse(session.Response))
        {
            var sessionData = sessionJson.RootElement.GetProperty("data");
            Assert.False(sessionData.GetProperty("started").GetBoolean());
            var balanced = sessionData.GetProperty("presets").EnumerateArray().Single(preset => preset.GetProperty("name").GetString() == "Balanced");
            Assert.Equal("RealPotential", balanced.GetProperty("people").GetString());
            Assert.Equal(5, balanced.GetProperty("history").GetInt32());
            Assert.Equal(3, sessionData.GetProperty("presets").GetArrayLength());
        }

        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var drivers = career.Host.Handle(Message("drivers", "query", "drivers"));
        using var driversJson = JsonDocument.Parse(drivers.Response);
        var own = driversJson.RootElement.GetProperty("data").GetProperty("own").EnumerateArray()
            .Select(driver => driver.GetProperty("name").GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var shown = card.GetProperty("drivers").EnumerateArray()
            .Select(driver => driver.GetProperty("name").GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(own, shown);
    }

    [Theory]
    [InlineData("Balanced", "Chaos")]
    [InlineData("Chaos", "Balanced")]
    public void TheTeamCardsOfOnePresetNameThePeopleThatPresetStartsWithEvenAfterAnotherPresetWasShown(string preset, string shownBefore)
    {
        // #255: the picker showed generated drivers while the started career had the real ones. The cards are read from the world
        // the career would start in, and a setup shown before must not leak into them.
        using var career = Lobby();
        string Args(string name) =>
            "{\"managerId\":\"human:player\",\"year\":1955,\"preset\":\"" + name + "\",\"people\":null,\"seed\":1}";
        var before = career.Host.Handle(Message("before", "query", "teams", Args(shownBefore)));
        Assert.True(JsonDocument.Parse(before.Response).RootElement.GetProperty("ok").GetBoolean(), before.Response);
        var cards = career.Host.Handle(Message("cards", "query", "teams", Args(preset)));
        using var cardsJson = JsonDocument.Parse(cards.Response);
        var card = cardsJson.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "maserati");

        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            "{\"managerId\":\"human:player\",\"teamId\":\"maserati\",\"givenName\":\"Enzo\",\"familyName\":\"Test\",\"nationality\":\"IT\",\"tilt\":\"none\",\"preset\":\"" + preset + "\",\"people\":null,\"year\":1955,\"seed\":1}"));
        Assert.True(JsonDocument.Parse(started.Response).RootElement.GetProperty("ok").GetBoolean(), started.Response);
        var drivers = career.Host.Handle(Message("drivers", "query", "drivers"));
        using var driversJson = JsonDocument.Parse(drivers.Response);
        var own = driversJson.RootElement.GetProperty("data").GetProperty("own").EnumerateArray()
            .Select(driver => driver.GetProperty("name").GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var shown = card.GetProperty("drivers").EnumerateArray()
            .Select(driver => driver.GetProperty("name").GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(own, shown);
    }

    [Fact]
    public void TheFinanceReadCarriesTheLatestLedgerLinesNewestFirst()
    {
        using var career = StartFerrariCareer();
        var finance = career.Host.Handle(Message("fin", "query", "finance"));
        using var json = JsonDocument.Parse(finance.Response);
        var ledger = json.RootElement.GetProperty("data").GetProperty("ledger");
        Assert.True(ledger.GetArrayLength() >= 1, finance.Response);
        var first = ledger[0];
        Assert.True(first.GetProperty("amountCents").GetInt64() != 0);
        Assert.StartsWith("finance.reason.", first.GetProperty("reason").GetProperty("key").GetString());
        Assert.False(first.TryGetProperty("counterparty", out _));
    }

    [Fact]
    public void AnUpgradeFromThePageStartsTheBuildChargesTheLedgerAndAnUnknownKindIsRefusedWithAReason()
    {
        using var career = StartFerrariCareer();
        using var before = JsonDocument.Parse(career.Host.Handle(Message("i1", "query", "infrastructure")).Response);
        var own = before.RootElement.GetProperty("data").GetProperty("own")[0];
        var organization = own.GetProperty("organizationId").GetString();
        var kind = own.GetProperty("facilities").EnumerateArray()
            .First(item => item.GetProperty("unlocked").GetBoolean()).GetProperty("kind").GetString();

        var refused = career.Host.Handle(Message(
            "bad", "command", "upgradeFacility",
            "{\"managerId\":\"human:player\",\"organizationId\":\"" + organization + "\",\"kind\":\"nonsense\"}"));
        using (var refusedJson = JsonDocument.Parse(refused.Response))
        {
            Assert.False(refusedJson.RootElement.GetProperty("ok").GetBoolean(), refused.Response);
            Assert.Equal("infrastructure.error.unknownKind", refusedJson.RootElement.GetProperty("error").GetProperty("key").GetString());
        }

        var started = career.Host.Handle(Message(
            "up", "command", "upgradeFacility",
            "{\"managerId\":\"human:player\",\"organizationId\":\"" + organization + "\",\"kind\":\"" + kind + "\"}"));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        using var after = JsonDocument.Parse(career.Host.Handle(Message("i2", "query", "infrastructure")).Response);
        var row = after.RootElement.GetProperty("data").GetProperty("own")[0].GetProperty("facilities").EnumerateArray()
            .First(item => item.GetProperty("kind").GetString() == kind);
        Assert.True(row.GetProperty("building").GetBoolean());
        using var finance = JsonDocument.Parse(career.Host.Handle(Message("fin", "query", "finance")).Response);
        Assert.Equal("infrastructure.ledger.upgrade", finance.RootElement.GetProperty("data").GetProperty("ledger")[0].GetProperty("reason").GetProperty("key").GetString());
    }

    [Fact]
    public void TheStandingsRulesNameTheCountingRuleByItsKindAndNeverPrintAnObject()
    {
        // #255: the counting rule was sent as the C# ToString of a record ("ResultsCountingRule { Kind = ... }").
        using var career = StartFerrariCareer();
        AdvanceUntilFirstRace(career.Host);
        var standings = career.Host.Handle(Message("standings", "query", "standings"));
        using var json = JsonDocument.Parse(standings.Response);
        var rules = json.RootElement.GetProperty("data").GetProperty("rules");
        Assert.Contains(rules.GetProperty("resultsCounting").GetString(), new[] { "All", "BestOverall", "Split" });
        Assert.DoesNotContain("{", rules.GetProperty("resultsCounting").GetString());
    }

    [Fact]
    public void SavingAgainUnderTheSameNameReplacesTheSaveAndTheListShowsWhoAndWhen()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var name = "bridge-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeTestData.SavesDirectory, name + ".paddock");
        var args = "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}";
        try
        {
            for (var round = 0; round < 2; round++)
            {
                var saved = career.Host.Handle(Message("save" + round, "command", "saveCareer", args));
                using var json = JsonDocument.Parse(saved.Response);
                Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), "save " + round + ": " + saved.Response);
            }

            var list = career.Host.Handle(Message("saves", "query", "saves"));
            using var listJson = JsonDocument.Parse(list.Response);
            var mine = listJson.RootElement.GetProperty("data").GetProperty("saves").EnumerateArray()
                .Single(save => save.GetProperty("name").GetString() == name + ".paddock");
            Assert.Equal("ferrari", mine.GetProperty("teamId").GetString());
            Assert.Equal("Enzo Test", mine.GetProperty("careerName").GetString());
            Assert.Equal("1955-01-01", mine.GetProperty("date").GetString());
            Assert.EndsWith("Z", mine.GetProperty("savedAt").GetString());
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            foreach (var leftover in new[] { path, path + ".tmp" })
            {
                if (File.Exists(leftover))
                {
                    File.Delete(leftover);
                }
            }
        }
    }

    [Fact]
    public void ADriverProfileGivesBandsAndContractOnlyForOwnDriversAndChangesNothing()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var hash = career.Host.StateHash;
        var drivers = career.Host.Handle(Message("drivers", "query", "drivers"));
        using var driversJson = JsonDocument.Parse(drivers.Response);
        var data = driversJson.RootElement.GetProperty("data");
        var ownId = data.GetProperty("own")[0].GetProperty("personId").GetString();
        var rivals = career.Host.Handle(Message("market", "query", "market"));
        using var marketJson = JsonDocument.Parse(rivals.Response);
        var rivalId = marketJson.RootElement.GetProperty("data").GetProperty("contracted")[0].GetProperty("personId").GetString();

        var own = career.Host.Handle(Message("own", "query", "driver", "{\"managerId\":\"human:player\",\"personId\":\"" + ownId + "\"}"));
        using (var json = JsonDocument.Parse(own.Response))
        {
            var profile = json.RootElement.GetProperty("data");
            Assert.True(profile.GetProperty("found").GetBoolean(), own.Response);
            Assert.True(profile.GetProperty("own").GetBoolean());
            Assert.Equal("ferrari", profile.GetProperty("organizationId").GetString());
            Assert.NotEqual(JsonValueKind.Null, profile.GetProperty("contract").ValueKind);
            Assert.True(profile.GetProperty("age").GetInt32() > 15);
            Assert.Equal(JsonValueKind.Array, profile.GetProperty("attributes").ValueKind);
            Assert.Empty(profile.GetProperty("seasons").EnumerateArray());
        }

        var rival = career.Host.Handle(Message("rival", "query", "driver", "{\"managerId\":\"human:player\",\"personId\":\"" + rivalId + "\"}"));
        using (var json = JsonDocument.Parse(rival.Response))
        {
            var profile = json.RootElement.GetProperty("data");
            Assert.True(profile.GetProperty("found").GetBoolean(), rival.Response);
            Assert.False(profile.GetProperty("own").GetBoolean());
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("contract").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, profile.GetProperty("contractEnd").ValueKind);
        }

        var nobody = career.Host.Handle(Message("nobody", "query", "driver", "{\"managerId\":\"human:player\",\"personId\":\"person:99999\"}"));
        using (var json = JsonDocument.Parse(nobody.Response))
        {
            Assert.False(json.RootElement.GetProperty("data").GetProperty("found").GetBoolean());
        }

        var staff = career.Host.Handle(Message("staff", "query", "staff"));
        using (var json = JsonDocument.Parse(staff.Response))
        {
            var person = json.RootElement.GetProperty("data").GetProperty("people").EnumerateArray().First(item => item.GetProperty("ownTeam").GetBoolean());
            Assert.False(string.IsNullOrEmpty(person.GetProperty("nationality").GetString()));
            Assert.True(person.GetProperty("age").GetInt32() > 15);
            Assert.NotEqual(JsonValueKind.Null, person.GetProperty("contractEnd").ValueKind);
            var other = json.RootElement.GetProperty("data").GetProperty("people").EnumerateArray().First(item => !item.GetProperty("ownTeam").GetBoolean());
            Assert.Equal(JsonValueKind.Null, other.GetProperty("contractEnd").ValueKind);
        }

        Assert.Equal(hash, career.Host.StateHash);
    }

    [Fact]
    public void TheManagerProfileIsThePrincipalTheCareerCreated()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"negotiation","preset":"Chaos","year":1955,"seed":1}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var hash = career.Host.StateHash;
        var reply = career.Host.Handle(Message("manager", "query", "manager"));
        using var json = JsonDocument.Parse(reply.Response);
        var profile = json.RootElement.GetProperty("data");
        Assert.True(profile.GetProperty("found").GetBoolean(), reply.Response);
        Assert.Equal("Enzo Test", profile.GetProperty("name").GetString());
        Assert.Equal("ITA", profile.GetProperty("nationality").GetString());
        Assert.Equal(40, profile.GetProperty("age").GetInt32());
        var negotiation = profile.GetProperty("attributes").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "negotiation");
        var politics = profile.GetProperty("attributes").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "politics");
        Assert.True(negotiation.GetProperty("low").GetInt32() > politics.GetProperty("low").GetInt32());
        Assert.Equal(hash, career.Host.StateHash);
    }

    [Fact]
    public void AnOpenNegotiationCanBeReadBack()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var startedJson = JsonDocument.Parse(started.Response))
        {
            Assert.True(startedJson.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        var market = career.Host.Handle(Message("market", "query", "market"));
        using var marketJson = JsonDocument.Parse(market.Response);
        var free = marketJson.RootElement.GetProperty("data").GetProperty("freeAgents").EnumerateArray().First(person => person.GetProperty("kind").GetString() == "driver").GetProperty("personId").GetString();
        var opened = career.Host.Handle(Message(
            "open",
            "command",
            "openNegotiation",
            "{\"managerId\":\"human:player\",\"organizationId\":\"ferrari\",\"personId\":\"" + free + "\",\"subject\":\"driver\",\"deadline\":null}"));
        using (var openedJson = JsonDocument.Parse(opened.Response))
        {
            Assert.True(openedJson.RootElement.GetProperty("ok").GetBoolean(), opened.Response);
        }

        var negotiations = career.Host.Handle(Message("neg", "query", "negotiations"));
        using var json = JsonDocument.Parse(negotiations.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), negotiations.Response);
        var item = Assert.Single(json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(free, item.GetProperty("person").GetString());

        var id = item.GetProperty("id").GetString();
        var offered = career.Host.Handle(Message(
            "offer",
            "command",
            "submitOffer",
            "{\"managerId\":\"human:player\",\"negotiationId\":\"" + id + "\",\"salary\":50000,\"winBonus\":0,\"titleBonus\":0,\"years\":1,\"seat\":\"Equal\",\"optionHolder\":null,\"optionYears\":null,\"exitWorseThan\":null}"));
        using (var offeredJson = JsonDocument.Parse(offered.Response))
        {
            Assert.True(offeredJson.RootElement.GetProperty("ok").GetBoolean(), offered.Response);
        }

        var after = career.Host.Handle(Message("neg2", "query", "negotiations"));
        using var afterJson = JsonDocument.Parse(after.Response);
        Assert.True(afterJson.RootElement.GetProperty("ok").GetBoolean(), after.Response);
        var round = afterJson.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("AwaitingResponse", round.GetProperty("status").GetString());
        Assert.Equal(50000, round.GetProperty("offer").GetProperty("salary").GetInt64());
    }

    [Fact]
    public void ASetupTheCareerWouldRefuseComesBackAsTheProblemOfTheTeamList()
    {
        using var career = Lobby();
        var exchange = career.Host.Handle(Message(
            "bad",
            "query",
            "teams",
            """{"managerId":"human:player","year":1955,"preset":"Chaos","people":null,"rules":"VotedEachSeason","ai":"ReplayHistory","history":null,"randomness":null,"fatality":null,"noNumbers":null,"seed":1}"""));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("config.error.replay_needs_historical_rules", data.GetProperty("problem").GetProperty("key").GetString());
        foreach (var team in data.GetProperty("teams").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, team.GetProperty("expected").ValueKind);
        }
    }

    [Fact]
    public async Task DevSocketRoundTripsAQuery()
    {
        using var career = Open();
        using var server = new DevServer(career.Host, Path.Combine(Path.GetTempPath(), "paddock-ui-missing"), DevServer.FindPort(4731));
        server.Start();
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(server.WebSocketUrl), CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(Message("s1", "query", "shell"));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[64 * 1024];
        var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, received.Count));
        Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    private static Opened StartFerrariCareer()
    {
        var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using var json = JsonDocument.Parse(started.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        return career;
    }

    private static void AdvanceUntilFirstRace(BridgeHost host)
    {
        var raced = false;
        for (var day = 0; day < 90 && !raced; day++)
        {
            var pool = host.Handle(Message("pool" + day, "query", "pool"));
            using (var poolJson = JsonDocument.Parse(pool.Response))
            {
                Assert.Equal(JsonValueKind.Null, poolJson.RootElement.GetProperty("data").GetProperty("focus").ValueKind);
            }

            ClearDecision(host, day);
            var moved = host.Handle(Message("day" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            Assert.True(movedJson.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
            var table = host.Handle(Message("table" + day, "query", "standings"));
            using var tableJson = JsonDocument.Parse(table.Response);
            Assert.True(tableJson.RootElement.GetProperty("ok").GetBoolean(), table.Response);
            if (tableJson.RootElement.GetProperty("data").GetProperty("roundsCompleted").GetInt32() >= 1)
            {
                raced = true;
            }
        }

        Assert.True(raced, "the first 1955 race did not finish");
    }

    private static void AssertCalendarKeepsFinishedRaceDate(BridgeHost host)
    {
        var calendar = host.Handle(Message("calendar-dates", "query", "calendar"));
        using var json = JsonDocument.Parse(calendar.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), calendar.Response);
        var rounds = json.RootElement.GetProperty("data").GetProperty("rounds");
        Assert.True(rounds.GetArrayLength() > 1, calendar.Response);
        foreach (var round in rounds.EnumerateArray())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(round.GetProperty("race").GetString()),
                "round " + round.GetProperty("round").GetInt32() + " lost its race date: " + calendar.Response);
        }

        var first = rounds.EnumerateArray().Single(round => round.GetProperty("round").GetInt32() == 1);
        Assert.True(first.GetProperty("finished").GetBoolean(), calendar.Response);
        Assert.Equal("1955-03-01", first.GetProperty("practice").GetString());
        Assert.Equal("1955-03-02", first.GetProperty("qualifying").GetString());
        Assert.Equal("1955-03-03", first.GetProperty("race").GetString());
    }

    private static void AssertPlayerRaceResultHasNoSpy(BridgeHost host)
    {
        var result = host.Handle(Message(
            "spy",
            "query",
            "raceResult",
            "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"season\":null,\"round\":null}"));
        using var json = JsonDocument.Parse(result.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), result.Response);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("found").GetBoolean(), result.Response);
        foreach (var section in data.GetProperty("sections").EnumerateArray())
        {
            var title = section.GetProperty("title").GetProperty("key").GetString();
            Assert.False(IsSpyKey(title), "raceResult sent the Spy section to the player: " + result.Response);
            foreach (var line in section.GetProperty("lines").EnumerateArray())
            {
                Assert.False(IsSpyKey(line.GetProperty("key").GetString()), result.Response);
                foreach (var arg in line.GetProperty("args").EnumerateArray())
                {
                    var name = arg.GetProperty("name").GetString();
                    Assert.NotEqual("showery", name);
                    Assert.NotEqual("onset", name);
                    Assert.NotEqual("peak", name);
                }
            }
        }
    }

    private static bool IsSpyKey(string? key) =>
        key is not null
        && (string.Equals(key, "race.section.spy", StringComparison.Ordinal)
            || string.Equals(key, "race.spy.weather", StringComparison.Ordinal)
            || key.Contains("spy", StringComparison.OrdinalIgnoreCase));

    private static void AcceptExpectedSeasonTarget(BridgeHost host, string id)
    {
        var inbox = host.Handle(Message(id, "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), inbox.Response);
        var itemId = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind)
            .GetProperty("id").GetString();
        var resolved = host.Handle(Message(
            id + "-answer",
            "command",
            "resolveInbox",
            "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + SeasonTarget.Expected + "\"}"));
        using var answered = JsonDocument.Parse(resolved.Response);
        Assert.True(answered.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";

    private static void ClearDecision(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var inbox = host.Handle(Message("inbox" + day + "-" + pass, "query", "inbox"));
            using var json = JsonDocument.Parse(inbox.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), inbox.Response);
            string? itemId = null;
            string? optionId = null;
            foreach (var item in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
            {
                if (!item.GetProperty("needsDecision").GetBoolean() || item.GetProperty("status").GetString() != "Open")
                {
                    continue;
                }

                var options = item.GetProperty("options");
                if (options.GetArrayLength() == 0)
                {
                    continue;
                }

                itemId = item.GetProperty("id").GetString();
                optionId = options[0].GetProperty("id").GetString();
                break;
            }

            if (itemId is null)
            {
                return;
            }

            var resolved = host.Handle(Message(
                "yes" + day + "-" + pass,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + optionId + "\"}"));
            using var resolvedJson = JsonDocument.Parse(resolved.Response);
            Assert.True(resolvedJson.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
        }
    }

    [Fact]
    public void ASeasonNeverStallsOnAQuestionThePlayerCannotSee()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"maserati","givenName":"Jan","familyName":"Test","nationality":"POL","tilt":"none","preset":"Balanced","year":1955,"seed":1}"""));
        using (var json = JsonDocument.Parse(started.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        string? date = "1955-01-01";
        for (var day = 0; day < 380 && string.CompareOrdinal(date, "1956-01-02") < 0; day++)
        {
            var step = career.Host.Handle(Message("adv" + day, "command", "advanceDay"));
            using var stepJson = JsonDocument.Parse(step.Response);
            if (stepJson.RootElement.GetProperty("ok").GetBoolean())
            {
                date = stepJson.RootElement.GetProperty("data").GetProperty("date").GetString();
                continue;
            }

            // Refused: the clock is held, so the player must be able to see and answer at least one open decision.
            var answered = AnswerEveryOpenDecisionWithItsLastOption(career.Host, day);
            Assert.True(answered > 0, "The clock was held on " + date + " but the player has no open decision: " + step.Response);
        }

        Assert.True(string.CompareOrdinal(date, "1956-01-02") >= 0, "The season stopped on " + date);
    }

    private static int AnswerEveryOpenDecisionWithItsLastOption(BridgeHost host, int day)
    {
        var inbox = host.Handle(Message("inbox-open" + day, "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        var answered = 0;
        foreach (var item in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            if (!item.GetProperty("needsDecision").GetBoolean() || item.GetProperty("status").GetString() != "Open")
            {
                continue;
            }

            var options = item.GetProperty("options");
            if (options.GetArrayLength() == 0)
            {
                continue;
            }

            var itemId = item.GetProperty("id").GetString();
            var optionId = options[options.GetArrayLength() - 1].GetProperty("id").GetString();
            host.Handle(Message(
                "answer" + day + itemId,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + optionId + "\"}"));
            answered++;
        }

        return answered;
    }

    /// <summary>The 1955 world every pinned date and state hash in this file was taken in (#323 moved the default to 2010).</summary>
    private const int PinnedYear = 1955;

    private static Opened Open() => new(BridgeHost.Open(BridgeTestData.DataRoot, PinnedYear));

    private static Opened Lobby() => new(BridgeHost.Lobby(BridgeTestData.DataRoot));

    private sealed class Opened : IDisposable
    {
        public Opened(BridgeHost host) => Host = host;

        public BridgeHost Host { get; }

        public void Dispose()
        {
        }
    }
}
