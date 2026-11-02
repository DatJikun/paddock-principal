using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// #320 (playtest 2): the menu badge showed 4 after the player had read an e-mail, because it counted open items, not unread ones.
/// The count is a query on the core; reading and deleting are commands; a save keeps both.
/// </summary>
public class InboxBridgeTests
{
    [Fact]
    public void TheUnreadCountFollowsReadingAndDeleting()
    {
        var host = StartCareer();
        var unread = Unread(host);
        var items = Items(host);
        Assert.True(unread >= 1, "the opening inbox has nothing unread");
        Assert.Equal(items.Count(item => item.Open && !item.Read), unread);

        var first = items.First(item => item.Open && !item.Read);
        Assert.True(Send(host, "read1", "command", "readInbox", Item(first.Id)).Ok);
        Assert.Equal(unread - 1, Unread(host));
        Assert.True(Items(host).Single(item => item.Id == first.Id).Read);

        // Reading it again changes nothing.
        var hash = host.StateHash;
        Assert.True(Send(host, "read2", "command", "readInbox", Item(first.Id)).Ok);
        Assert.Equal(unread - 1, Unread(host));
        Assert.Equal(hash, host.StateHash);

        // A deleted notice leaves the count whether it was read or not.
        var notice = NoticeAfterSomeDays(host);
        var before = Unread(host);
        Assert.True(Send(host, "del", "command", "dismissInbox", Item(notice)).Ok);
        Assert.Equal(before - 1, Unread(host));
    }

    [Fact]
    public void AnotherManagersItemCannotBeMarkedRead()
    {
        var host = StartCareer();
        var hash = host.StateHash;
        var (ok, _) = Send(host, "read", "command", "readInbox", Item("inb:999999"));
        Assert.False(ok);
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void ReadAndDeletedStateSurvivesSaveAndResume()
    {
        var host = StartCareer();
        var notice = NoticeAfterSomeDays(host);
        var read = Items(host).First(item => item.Open && !item.Read && item.Id != notice);
        Assert.True(Send(host, "read", "command", "readInbox", Item(read.Id)).Ok);
        Assert.True(Send(host, "del", "command", "dismissInbox", Item(notice)).Ok);
        var unread = Unread(host);
        var hash = host.StateHash;

        var name = "inbox-read-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeTestData.SavesDirectory, name + ".paddock");
        try
        {
            Assert.True(Send(host, "save", "command", "saveCareer", "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}").Ok);
            var resumed = BridgeHost.Lobby(BridgeTestData.DataRoot);
            Assert.True(Send(resumed, "load", "command", "loadCareer", "{\"managerId\":\"human:player\",\"path\":\"" + name + "\"}").Ok);

            Assert.Equal(hash, resumed.StateHash);
            Assert.Equal(unread, Unread(resumed));
            var items = Items(resumed);
            Assert.True(items.Single(item => item.Id == read.Id).Read);
            Assert.Equal("Dismissed", items.Single(item => item.Id == notice).Status);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static BridgeHost StartCareer()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var (ok, response) = Send(
            host,
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Balanced","year":1955,"seed":3}""");
        Assert.True(ok, response);
        return host;
    }

    /// <summary>Advances day by day (answering decisions with their default) until an open, unread notice is in the inbox.</summary>
    private static string NoticeAfterSomeDays(BridgeHost host)
    {
        for (var day = 0; day < 120; day++)
        {
            var notice = Items(host).FirstOrDefault(item => item.Open && !item.Decision && !item.Read);
            if (notice is not null)
            {
                return notice.Id;
            }

            foreach (var decision in Items(host).Where(item => item.Open && item.Decision))
            {
                Send(host, "answer" + day + decision.Id, "command", "resolveInbox", "{\"managerId\":\"human:player\",\"itemId\":\"" + decision.Id + "\",\"optionId\":\"" + decision.FirstOption + "\"}");
            }

            Assert.True(Send(host, "day" + day, "command", "advanceDay").Ok);
        }

        Assert.Fail("no notice arrived");
        return "";
    }

    private static int Unread(BridgeHost host)
    {
        using var json = JsonDocument.Parse(host.Handle(Message("shell", "query", "shell")).Response);
        return json.RootElement.GetProperty("data").GetProperty("inboxUnread").GetInt32();
    }

    private static List<Row> Items(BridgeHost host)
    {
        using var json = JsonDocument.Parse(host.Handle(Message("inbox", "query", "inbox")).Response);
        var data = json.RootElement.GetProperty("data");
        var rows = data.GetProperty("items").EnumerateArray()
            .Select(item => new Row(
                item.GetProperty("id").GetString()!,
                item.GetProperty("status").GetString()!,
                item.GetProperty("read").GetBoolean(),
                item.GetProperty("needsDecision").GetBoolean(),
                item.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()!).FirstOrDefault()))
            .ToList();
        Assert.Equal(rows.Count(row => row.Open && !row.Read), data.GetProperty("unreadCount").GetInt32());
        return rows;
    }

    private static string Item(string id) => "{\"managerId\":\"human:player\",\"itemId\":\"" + id + "\"}";

    private static (bool Ok, string Response) Send(BridgeHost host, string id, string kind, string name, string? args = null)
    {
        var response = host.Handle(Message(id, kind, name, args)).Response;
        using var json = JsonDocument.Parse(response);
        return (json.RootElement.GetProperty("ok").GetBoolean(), response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";

    private sealed record Row(string Id, string Status, bool Read, bool Decision, string? FirstOption)
    {
        public bool Open => Status == "Open";
    }
}
