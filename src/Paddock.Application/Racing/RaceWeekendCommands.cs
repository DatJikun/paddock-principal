using System.Collections.Immutable;
using System.Text.Json;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Practice;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// The sessions of today's weekend before the race (#316): the input the race runs from, its field, practice, qualifying, the tyres
/// on offer and the pit rules. Pure output of <see cref="RaceWeekendDay.Sessions"/>.
/// </summary>
public sealed record WeekendSessions(
    RaceWeekendInput Input,
    RaceField Field,
    PracticeFacts Practice,
    QualifyingResult Qualifying,
    TyreOffer Tyres,
    PitRules Pits);

/// <summary>
/// A team's plan for one of its cars in today's race (#316): start tyres, pace, engine mode and the stops (<see cref="Stops"/> in the
/// text form of <see cref="RaceWeekendSection.StopsText"/>; <c>*</c> leaves them to the strategist). Refused once the race is being
/// watched, since the plan acts from lap 1.
/// </summary>
public sealed record SetRacePlanCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required int Season { get; init; }

    public required int Round { get; init; }

    public required string CarId { get; init; }

    public string? StartCompoundId { get; init; }

    public string? Pace { get; init; }

    public string Engine { get; init; } = nameof(EngineMode.Standard);

    public string Stops { get; init; } = "*";

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>The players opened today's race to watch it (#316); <see cref="TapeHash"/> is the tape they see.</summary>
public sealed record WatchRaceCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required int Season { get; init; }

    public required int Round { get; init; }

    public required string TapeHash { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The pit wall orders of a manager's cars in today's watched race (#316), the whole list after one more order or a cancelled
/// stop, and the tape they make. The host checks an order against the race clock before it sends this (no past may change); the
/// world keeps the list, so the day books the race with it and the command log replays it (INV-002).
/// </summary>
public sealed record SetPitWallOrdersCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required int Season { get; init; }

    public required int Round { get; init; }

    public required IReadOnlyList<WeekendOrder> Orders { get; init; }

    public required string TapeHash { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record RacePlanSet(ManagerId ManagerId, DateOnly OccurredOn, string CarId) : IDomainEvent
{
    public string TypeId => "racing.planSet";
}

public sealed record RaceWatched(ManagerId ManagerId, DateOnly OccurredOn, int Season, int Round) : IDomainEvent
{
    public string TypeId => "racing.watched";
}

public sealed record PitWallOrdersSet(ManagerId ManagerId, DateOnly OccurredOn, int Orders) : IDomainEvent
{
    public string TypeId => "racing.ordersSet";
}

/// <summary>Player text of the race weekend (#316).</summary>
public static class RaceWeekendKeys
{
    public const string NoRace = "race.weekend.noRace";
    public const string NotOwn = "race.weekend.notOwn";
    public const string Started = "race.weekend.started";
    public const string BadTyre = "race.weekend.badTyre";
    public const string BadStops = "race.weekend.badStops";
    public const string NeedsMix = "race.weekend.needsMix";
    public const string NoTyreChange = "race.weekend.noTyreChange";
    public const string BadMode = "race.weekend.badMode";

    /// <summary>Inbox notice: the race the day booked is not the one the players watched.</summary>
    public const string RerunKind = "race.rerun";
    public const string RerunSubject = "race.rerun.subject";

    public static IReadOnlyList<string> All { get; } = [NoRace, NotOwn, Started, BadTyre, BadStops, NeedsMix, NoTyreChange, BadMode, RerunSubject];
}

/// <summary>Rules of a pre-race plan. ESTIMATE where marked.</summary>
public static class RacePlanRules
{
    /// <summary>ESTIMATE: the most stops a plan may name.</summary>
    public const int MaxStops = 4;

    /// <summary>The refusal key of <paramref name="plan"/> in <paramref name="sessions"/>, or null when the engine can run it.</summary>
    public static string? Check(RacePlan plan, WeekendSessions sessions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sessions);
        var offer = sessions.Tyres;
        var dry = offer.Compounds.Where(id => id != offer.WetCompound).ToArray();
        if (plan.StartCompoundId is { } start && !dry.Contains(start, StringComparer.Ordinal))
        {
            return RaceWeekendKeys.BadTyre;
        }

        if (plan.Stops is not { } stops)
        {
            return null;
        }

        if (stops.Length > MaxStops)
        {
            return RaceWeekendKeys.BadStops;
        }

        if (stops.Length > 0 && !offer.TyreChange)
        {
            return RaceWeekendKeys.NoTyreChange;
        }

        var last = 0;
        foreach (var stop in stops)
        {
            if (stop.Lap <= last || stop.Lap >= sessions.Input.TotalLaps)
            {
                return RaceWeekendKeys.BadStops;
            }

            if (!offer.Compounds.Contains(stop.CompoundId, StringComparer.Ordinal))
            {
                return RaceWeekendKeys.BadTyre;
            }

            last = stop.Lap;
        }

        var used = stops.Select(stop => stop.CompoundId).Append(plan.StartCompoundId ?? offer.StartCompound)
            .Where(id => dry.Contains(id, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return used < sessions.Pits.RequiredDistinctDryCompounds ? RaceWeekendKeys.NeedsMix : null;
    }
}

/// <summary>What the weekend commands share: today's race, the manager's team and its cars in the field.</summary>
internal static class RaceWeekendSupport
{
    /// <summary>The race scheduled for today, or null.</summary>
    public static RaceSessionPayload? Today(CareerSession session)
    {
        foreach (var scheduled in session.Clock.Queue.Events)
        {
            if (scheduled.TypeId == ScheduledEventType.Race && scheduled.Date == session.Date && scheduled.Payload is RaceSessionPayload payload)
            {
                return payload;
            }
        }

        return null;
    }

    /// <summary>Today's sessions when (season, round) is today's race; the refusal key otherwise.</summary>
    public static WeekendSessions? Sessions(CareerModuleContext modules, int season, int round, out string? refusal)
    {
        refusal = RaceWeekendKeys.NoRace;
        if (Today(modules.Session) is not { } payload || payload.Season != season || payload.Round != round)
        {
            return null;
        }

        var sessions = new RaceWeekendDay(modules, modules.Require<RaceWatch>()).Sessions(modules.Session.Date, payload);
        refusal = sessions is null ? RaceWeekendKeys.NoRace : null;
        return sessions;
    }

    /// <summary>The car ids (tape ids) of the teams <paramref name="manager"/> runs in the field.</summary>
    public static IReadOnlySet<string> OwnCars(CareerModuleContext modules, ManagerId manager, RaceField field)
    {
        var control = modules.TryGet<ControlTable>() ?? modules.TryGet<IOrganizationControl>();
        var cars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in field.Entries)
        {
            if (control is not null
                && Paddock.Application.Cars.CarCommandSupport.TryOrganization(entry.ConstructorId, out var organization)
                && control.Controls(manager, organization))
            {
                cars.Add(entry.Primary.DriverId);
            }
        }

        return cars;
    }

    public static RaceWeekendSection Section(CareerModuleContext modules, int season, int round) =>
        RaceWeekendState.Of(modules.Session, season, round) ?? RaceWeekendSection.Empty(season, round);
}

public sealed class SetRacePlanHandler(CareerModuleContext modules) : CommandHandler<SetRacePlanCommand>
{
    protected override TranslationMessage? ValidateTyped(SetRacePlanCommand command, CommandContext context)
    {
        if (RaceWeekendSupport.Sessions(modules, command.Season, command.Round, out var refusal) is not { } sessions)
        {
            return TranslationMessage.Of(refusal!);
        }

        if (!RaceWeekendSupport.OwnCars(modules, command.ManagerId, sessions.Field).Contains(command.CarId))
        {
            return TranslationMessage.Of(RaceWeekendKeys.NotOwn);
        }

        if (RaceWeekendState.Of(modules.Session, command.Season, command.Round)?.WatchedTapeHash is not null)
        {
            return TranslationMessage.Of(RaceWeekendKeys.Started);
        }

        if (Plan(command) is not { } plan)
        {
            return TranslationMessage.Of(RaceWeekendKeys.BadMode);
        }

        return RacePlanRules.Check(plan, sessions) is { } key ? TranslationMessage.Of(key) : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SetRacePlanCommand command, CommandContext context)
    {
        var sessions = RaceWeekendSupport.Sessions(modules, command.Season, command.Round, out _)
            ?? throw new InvalidOperationException("A validated plan lost its race.");
        var team = sessions.Field.Entries.First(entry => entry.Primary.DriverId == command.CarId).ConstructorId;
        var section = RaceWeekendSupport.Section(modules, command.Season, command.Round)
            .WithPlan(RaceWeekendState.FromPlan(Plan(command)!, team));
        RaceWeekendState.Store(modules, section);
        return [new RacePlanSet(command.ManagerId, command.IssuedOn, command.CarId)];
    }

    /// <summary>The command as the engine's plan; null when a mode or the stops do not parse.</summary>
    internal static RacePlan? Plan(SetRacePlanCommand command)
    {
        PaceMode? pace = null;
        if (command.Pace is { } paceText)
        {
            if (!Enum.TryParse<PaceMode>(paceText, out var parsed) || !Enum.IsDefined(parsed))
            {
                return null;
            }

            pace = parsed;
        }

        if (!Enum.TryParse<EngineMode>(command.Engine, out var engine) || !Enum.IsDefined(engine))
        {
            return null;
        }

        IReadOnlyList<WeekendPlannedStop>? stops;
        try
        {
            stops = RaceWeekendSection.ParseStops(command.Stops);
        }
        catch (FormatException)
        {
            return null;
        }

        return new RacePlan(
            command.CarId,
            string.IsNullOrEmpty(command.StartCompoundId) ? null : command.StartCompoundId,
            pace,
            engine,
            stops is null ? null : [.. stops.Select(stop => new PlannedStop(stop.Lap, stop.CompoundId))]);
    }
}

public sealed class WatchRaceHandler(CareerModuleContext modules) : CommandHandler<WatchRaceCommand>
{
    protected override TranslationMessage? ValidateTyped(WatchRaceCommand command, CommandContext context) =>
        RaceWeekendSupport.Today(modules.Session) is { } today && today.Season == command.Season && today.Round == command.Round
            && !string.IsNullOrWhiteSpace(command.TapeHash)
            ? null
            : TranslationMessage.Of(RaceWeekendKeys.NoRace);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(WatchRaceCommand command, CommandContext context)
    {
        RaceWeekendState.Store(modules, RaceWeekendSupport.Section(modules, command.Season, command.Round).WithWatched(command.TapeHash));
        return [new RaceWatched(command.ManagerId, command.IssuedOn, command.Season, command.Round)];
    }
}

public sealed class SetPitWallOrdersHandler(CareerModuleContext modules) : CommandHandler<SetPitWallOrdersCommand>
{
    protected override TranslationMessage? ValidateTyped(SetPitWallOrdersCommand command, CommandContext context)
    {
        if (RaceWeekendSupport.Sessions(modules, command.Season, command.Round, out var refusal) is not { } sessions)
        {
            return TranslationMessage.Of(refusal!);
        }

        if (string.IsNullOrWhiteSpace(command.TapeHash))
        {
            return TranslationMessage.Of(RaceWeekendKeys.NoRace);
        }

        var own = RaceWeekendSupport.OwnCars(modules, command.ManagerId, sessions.Field);
        foreach (var order in command.Orders)
        {
            if (!own.Contains(order.CarId))
            {
                return TranslationMessage.Of(RaceWeekendKeys.NotOwn);
            }

            try
            {
                _ = RaceWeekendState.ToOrder(order);
            }
            catch (ArgumentException)
            {
                return TranslationMessage.Of(RaceWeekendKeys.BadMode);
            }

            if (order.Lap < 1 || order.Lap > sessions.Input.TotalLaps)
            {
                return TranslationMessage.Of(RaceWeekendKeys.BadStops);
            }
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SetPitWallOrdersCommand command, CommandContext context)
    {
        var sessions = RaceWeekendSupport.Sessions(modules, command.Season, command.Round, out _)
            ?? throw new InvalidOperationException("Validated orders lost their race.");
        var own = RaceWeekendSupport.OwnCars(modules, command.ManagerId, sessions.Field);
        var section = RaceWeekendSupport.Section(modules, command.Season, command.Round).WithOrders(own, command.Orders, command.TapeHash);
        RaceWeekendState.Store(modules, section);
        return [new PitWallOrdersSet(command.ManagerId, command.IssuedOn, command.Orders.Count)];
    }
}

/// <summary>Save codecs of the race weekend commands (#316).</summary>
public static class RaceWeekendCommandCodecs
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<SetRacePlanCommand>(
            "racing.setPlan/1",
            command => Paddock.Simulation.Codec.FlatJson.Write(
                ("season", command.Season),
                ("round", command.Round),
                ("car", command.CarId),
                ("start", command.StartCompoundId ?? ""),
                ("pace", command.Pace ?? ""),
                ("engine", command.Engine),
                ("stops", command.Stops)),
            (body, manager, issued) =>
            {
                var fields = Paddock.Simulation.Codec.FlatJson.Read(body, "season", "round", "car", "start", "pace", "engine", "stops");
                return new SetRacePlanCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Season = fields.Int32("season"),
                    Round = fields.Int32("round"),
                    CarId = fields.String("car"),
                    StartCompoundId = Optional(fields.String("start")),
                    Pace = Optional(fields.String("pace")),
                    Engine = fields.String("engine"),
                    Stops = fields.String("stops"),
                };
            }),
        CommandCodecEntry.For<WatchRaceCommand>(
            "racing.watch/1",
            command => Paddock.Simulation.Codec.FlatJson.Write(("season", command.Season), ("round", command.Round), ("tape", command.TapeHash)),
            (body, manager, issued) =>
            {
                var fields = Paddock.Simulation.Codec.FlatJson.Read(body, "season", "round", "tape");
                return new WatchRaceCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Season = fields.Int32("season"),
                    Round = fields.Int32("round"),
                    TapeHash = fields.String("tape"),
                };
            }),
        CommandCodecEntry.For<SetPitWallOrdersCommand>(
            "racing.setOrders/1",
            command => Paddock.Simulation.Codec.FlatJson.Write(
                ("season", command.Season),
                ("round", command.Round),
                ("tape", command.TapeHash),
                ("orders", JsonSerializer.Serialize(command.Orders.ToArray(), Json))),
            (body, manager, issued) =>
            {
                var fields = Paddock.Simulation.Codec.FlatJson.Read(body, "season", "round", "tape", "orders");
                return new SetPitWallOrdersCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Season = fields.Int32("season"),
                    Round = fields.Int32("round"),
                    TapeHash = fields.String("tape"),
                    Orders = JsonSerializer.Deserialize<WeekendOrder[]>(fields.String("orders"), Json)
                        ?? throw new InvalidDataException("A pit wall orders command has no orders."),
                };
            }),
    ];

    private static string? Optional(string text) => text.Length == 0 ? null : text;
}

/// <summary>Registers the race weekend commands (#316).</summary>
public static class RaceWeekendRegistration
{
    public static void Register(CommandDispatcher dispatcher, CareerModuleContext modules)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(modules);
        dispatcher.Register(new SetRacePlanHandler(modules));
        dispatcher.Register(new WatchRaceHandler(modules));
        dispatcher.Register(new SetPitWallOrdersHandler(modules));
    }
}

/// <summary>The issued orders of a watched race as the world command keeps them: only the manager's own cars.</summary>
public static class PitWallBooking
{
    public static IReadOnlyList<WeekendOrder> OwnOrders(ImmutableArray<IssuedOrder> issued, IReadOnlySet<string> ownCars) =>
        [.. issued.Where(order => ownCars.Contains(order.Order.CarId)).Select(RaceWeekendState.FromOrder)];
}
