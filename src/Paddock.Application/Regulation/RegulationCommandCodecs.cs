using Paddock.Application.Commands;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Regulation;

/// <summary>Save codecs of the regulation commands. <c>CommandCodec.Production</c> lists them.</summary>
public static class RegulationCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<ProposeRuleChangeCommand>(
            "regulation.propose/1",
            command => FlatJson.Write(("series", command.SeriesId), ("team", command.TeamId), ("dimension", command.DimensionId), ("value", command.Value)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "series", "team", "dimension", "value");
                return new ProposeRuleChangeCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    SeriesId = fields.String("series"),
                    TeamId = fields.String("team"),
                    DimensionId = fields.String("dimension"),
                    Value = fields.String("value"),
                };
            }),
        CommandCodecEntry.For<CastVoteCommand>(
            "regulation.vote/1",
            command => FlatJson.Write(("series", command.SeriesId), ("team", command.TeamId), ("item", command.ItemId), ("option", command.Option), ("spent", command.Spent)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "series", "team", "item", "option", "spent");
                return new CastVoteCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    SeriesId = fields.String("series"),
                    TeamId = fields.String("team"),
                    ItemId = fields.String("item"),
                    Option = fields.String("option"),
                    Spent = fields.Int32("spent"),
                };
            }),
    ];
}
