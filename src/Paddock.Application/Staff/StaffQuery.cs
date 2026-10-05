using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Staff;

/// <summary>One attribute the observer believes. Own staff are known exactly, so the band is a point (INV-003).</summary>
public sealed record StaffAttributeView(string Key, int Low, int High);

/// <summary>
/// One person on a team roster. Rivals are names and roles only: attributes and the engineer relationship stay null.
/// The team principal and the engine designer are not on this roster.
/// </summary>
public sealed record StaffPersonView(
    string PersonId,
    string Name,
    string Role,
    string OrganizationId,
    bool OwnTeam,
    IReadOnlyList<StaffAttributeView>? Attributes,
    string? DriverId,
    int? Relationship);

/// <summary>The staff a manager can see on one day. Reads no truth about a rival and draws no random numbers (INV-003, INV-005).</summary>
public static class StaffQuery
{
    public static IReadOnlyList<StaffPersonView> Of(WorldState world, OrganizationId observer, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!observer.IsAssigned)
        {
            throw new ArgumentException("Observer id is unassigned.", nameof(observer));
        }

        var links = world.Section<StaffSection>(StaffSection.SectionName) ?? StaffSection.Empty;
        var rows = new List<StaffPersonView>();
        foreach (var contract in world.Contracts)
        {
            if (!contract.IsActiveOn(on) || !contract.Role.IsStaff || !StaffCatalogue.IsTeamRoster(contract.Role.StaffRole))
            {
                continue;
            }

            var person = world.GetPerson(contract.PersonId);
            if (person.IsRetired)
            {
                continue;
            }

            var own = contract.OrganizationId == observer;
            var link = links.OfEngineer(person.Id);
            var pairedHere = link is not null && link.Team == contract.OrganizationId;
            IReadOnlyList<StaffAttributeView>? attributes = null;
            if (own && world.KnowledgeOf(observer, person.Id) is PersonKnowledgeView knowledge)
            {
                attributes = knowledge.Attributes
                    .Select(attribute => new StaffAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High))
                    .ToArray();
            }

            rows.Add(new StaffPersonView(
                person.Id.Value,
                person.Name,
                contract.Role.StaffRole.ToString(),
                contract.OrganizationId.Value,
                own,
                attributes,
                pairedHere ? link!.Driver.Value : null,
                own && pairedHere ? link!.Relationship : null));
        }

        return rows
            .OrderBy(row => row.OrganizationId, StringComparer.Ordinal)
            .ThenBy(row => row.Role, StringComparer.Ordinal)
            .ThenBy(row => row.PersonId, StringComparer.Ordinal)
            .ToArray();
    }
}
