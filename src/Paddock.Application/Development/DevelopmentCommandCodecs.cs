using Paddock.Application.Commands;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Development;

/// <summary>Save codecs of the development commands. <c>CommandCodec.Production</c> lists them.</summary>
public static class DevelopmentCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<SetDevelopmentSplitCommand>(
            "development.setSplit/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("current", command.CurrentPercent),
                ("account", command.AccountPercent),
                ("nextYear", command.NextYearPercent),
                ("aero", command.AeroPriority),
                ("chassis", command.ChassisPriority),
                ("reliability", command.ReliabilityPriority),
                ("tyres", command.TyresPriority)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "current", "account", "nextYear", "aero", "chassis", "reliability", "tyres");
                return new SetDevelopmentSplitCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    CurrentPercent = fields.Int32("current"),
                    AccountPercent = fields.Int32("account"),
                    NextYearPercent = fields.Int32("nextYear"),
                    AeroPriority = fields.Int32("aero"),
                    ChassisPriority = fields.Int32("chassis"),
                    ReliabilityPriority = fields.Int32("reliability"),
                    TyresPriority = fields.Int32("tyres"),
                };
            }),
        CommandCodecEntry.For<DeployConceptCommand>(
            "development.deployConcept/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("project", command.ProjectId),
                ("timing", command.Timing),
                ("races", command.Races)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "project", "timing", "races");
                return new DeployConceptCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    ProjectId = fields.String("project"),
                    Timing = fields.String("timing"),
                    Races = fields.Int32("races"),
                };
            }),
        CommandCodecEntry.For<CutProjectCommand>(
            "development.cutProject/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("project", command.ProjectId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "project");
                return new CutProjectCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    ProjectId = fields.String("project"),
                };
            }),
    ];
}
