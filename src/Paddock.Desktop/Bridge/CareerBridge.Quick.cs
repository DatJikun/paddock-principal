using System.Text.Json;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Racing;
using Paddock.Career;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.World;

namespace Paddock.Desktop.Bridge;

/// <summary>Argument of <c>quickRounds</c>: the season whose calendar the quick race offers.</summary>
public sealed record QuickRoundsCall(string ManagerId, int Year);

/// <summary>Argument of <c>startQuickRace</c>. Without <see cref="Seed"/> the race uses <see cref="CareerBridge.DefaultSeed"/>.</summary>
public sealed record QuickRaceCall(string ManagerId, int Year, string TeamId, int Round, ulong? Seed);

/// <summary>What <c>startQuickRace</c> returns: the race that is now open for watching.</summary>
public sealed record QuickRaceStartedView(int Season, int Round, string LayoutId, string OrganizationId);

/// <summary>
/// The quick race (#280): one round outside the career. The world is built as <c>newCareer</c> builds it (with the career
/// defaults of <see cref="QuickRacePreset"/>), the player sits on the chosen team, and the round is raced at once by the
/// career's own race day. It is held next to the career, never instead of it: the career in memory is untouched and nothing
/// is saved. While a quick race is open, the race mode reads it; closing it gives the race mode back to the career.
/// </summary>
public sealed partial class CareerBridge
{
    /// <summary>The new-career wizard's default preset: a quick race starts in the world a career would start in.</summary>
    public const CareerPreset QuickRacePreset = CareerPreset.Balanced;

    private CareerShell? _quick;
    private LiveRacePlayback? _quickLive;

    public bool HasQuickRace => _quick is not null;

    /// <summary>Whether <paramref name="name"/> can be read now. Without a career only the menu's reads and an open quick race are.</summary>
    public bool CanRead(string name) =>
        HasCareer
        || name is "session" or "teams" or "saves" or "quickRounds"
        || (HasQuickRace && name is "liveRace" or "liveFrames" or "liveClock" or "track");

    private QuickRoundsView ReadQuickRounds(JsonElement args)
    {
        var year = IntOf(args, "year") ?? DefaultYear;
        var data = AuthoredDataLoader.Load(RequireData());
        var (circuits, tracks) = CircuitsOf(data);
        return QuickRace.Rounds(year, data.Layouts, data.RaceAssignments, CareerData.LoadRaceDates(RequireData()), circuits, tracks);
    }

    private PlayStep StartQuickRace(JsonElement args)
    {
        var team = TextOf(args, "teamId");
        var year = IntOf(args, "year");
        var round = IntOf(args, "round");
        if (team is null || year is not int season || round is not int number)
        {
            return PlayStep.Fail(TranslationMessage.Of(BridgeKeys.BadMessage));
        }

        if (Configure(args, team, season, out var config, out var root, out var files, out _, QuickRacePreset) is { } refused)
        {
            return PlayStep.Fail(refused);
        }

        try
        {
            var (shell, data, created) = OpenShell(config, root, files, ULongOf(args, "seed") ?? DefaultSeed, "Principal");
            if (!created.PlayerOrganization.IsAssigned)
            {
                return PlayStep.Fail(TranslationMessage.Of(QuickRaceKeys.NoTeam, ("teamId", team)));
            }

            shell.Modules.Require<BoardEngine>().AppointHuman(
                shell.Player,
                created.PlayerOrganization,
                shell.Date,
                founder: false,
                BridgeKeys.CareerAppointed);
            if (QuickRace.Run(shell.Modules, number) is { } notRaced)
            {
                return PlayStep.Fail(TranslationMessage.Of(notRaced, ("round", number.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            }

            var watch = shell.Modules.Require<RaceWatch>();
            if (watch.Tape is not { } tape || tape.Events.IsDefaultOrEmpty)
            {
                return PlayStep.Fail(TranslationMessage.Of(QuickRaceKeys.NotRaced, ("round", number.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            }

            _quick = shell;
            _quickLive = new LiveRacePlayback(watch.Season, watch.Round, tape.Events[^1].RaceTime, LiveClock);
            RememberCircuits(data);
            return PlayStep.Ok(
                BridgeValues.ToNode(new QuickRaceStartedView(watch.Season, watch.Round, watch.LayoutId, created.PlayerOrganization.Value)),
                inbox: false);
        }
        catch (WorldInitException ex)
        {
            return PlayStep.Fail(TranslationMessage.Of(ex.Code));
        }
    }

    private void CloseQuickRace()
    {
        _quick = null;
        _quickLive = null;
    }
}
