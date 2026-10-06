using Paddock.Application.Commands;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Infrastructure;

/// <summary>Save codecs of the infrastructure commands. <c>CommandCodec.Production</c> lists them.</summary>
public static class InfrastructureCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<UpgradeFacilityCommand>(
            "infrastructure.upgrade/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("kind", command.Kind)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "kind");
                return new UpgradeFacilityCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    Kind = fields.String("kind"),
                };
            }),
        CommandCodecEntry.For<BookTestCommand>(
            "infrastructure.bookTest/1",
            command => FlatJson.Write(("organization", command.OrganizationId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization");
                return new BookTestCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                };
            }),
    ];
}
