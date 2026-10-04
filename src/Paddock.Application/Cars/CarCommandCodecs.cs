using Paddock.Application.Cars;
using Paddock.Application.Managers;
using Paddock.Domain.Codec;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Commands;

public static class CarCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<ApproveConceptCommand>(
            "car.approveConcept/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("aero", command.AeroMilli),
                ("philosophy", command.PhilosophyMilli),
                ("window", command.WindowMilli),
                ("cooling", command.CoolingMilli),
                ("tyre", command.TyreMilli),
                ("integration", command.IntegrationMilli)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "aero", "philosophy", "window", "cooling", "tyre", "integration");
                return new ApproveConceptCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    AeroMilli = fields.Int32("aero"),
                    PhilosophyMilli = fields.Int32("philosophy"),
                    WindowMilli = fields.Int32("window"),
                    CoolingMilli = fields.Int32("cooling"),
                    TyreMilli = fields.Int32("tyre"),
                    IntegrationMilli = fields.Int32("integration"),
                };
            }),
        CommandCodecEntry.For<AcquireCustomerCarCommand>(
            "car.acquireCustomer/1",
            command => FlatJson.Write(("organization", command.OrganizationId), ("seller", command.SellerId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "seller");
                return new AcquireCustomerCarCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    SellerId = fields.String("seller"),
                };
            }),
    ];
}
