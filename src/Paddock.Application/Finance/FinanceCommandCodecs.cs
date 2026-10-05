using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Finance;

public static class FinanceCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<OpenBooksCommand>(
            "finance.openBooks/1",
            command => FlatJson.Write(("organization", command.OrganizationId)),
            (body, manager, issued) => new OpenBooksCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                OrganizationId = FlatJson.Read(body, "organization").String("organization"),
            }),
        CommandCodecEntry.For<ApplyRaceResultsCommand>(
            "finance.race/1",
            command => FlatJson.Write(
                ("season", command.Season),
                ("round", command.Round),
                ("races", command.RacesInSeason),
                ("entries", command.Entries)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "season", "round", "races", "entries");
                return new ApplyRaceResultsCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Season = fields.Int32("season"),
                    Round = fields.Int32("round"),
                    RacesInSeason = fields.Int32("races"),
                    Entries = fields.String("entries"),
                };
            }),
        CommandCodecEntry.For<ApplySeasonEndedCommand>(
            "finance.season/1",
            command => FlatJson.Write(
                ("season", command.Season),
                ("races", command.Races),
                ("constructors", command.Constructors),
                ("winners", command.Winners),
                ("drivers", command.Drivers)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "season", "races", "constructors", "winners", "drivers");
                return new ApplySeasonEndedCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Season = fields.Int32("season"),
                    Races = fields.Int32("races"),
                    Constructors = fields.String("constructors"),
                    Winners = fields.String("winners"),
                    Drivers = fields.String("drivers"),
                };
            }),
    ];
}
