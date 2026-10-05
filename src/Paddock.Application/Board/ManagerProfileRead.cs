using Paddock.Application.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Board;

/// <summary>
/// The principal who runs the observer's team, as the team knows him: who he is and the attributes the team believes about him.
/// <see cref="Since"/> is the day his contract as principal began.
/// </summary>
public sealed record ManagerProfileView(
    bool Found,
    string PersonId,
    string Name,
    string Nationality,
    int Age,
    string? Since,
    IReadOnlyList<KnownAttributeView> Attributes);

/// <summary>Reads the team principal of the observer's own team. A pure query: no state change and no random number (INV-005).</summary>
public static class ManagerProfileRead
{
    public static ManagerProfileView None { get; } = new(false, "", "", "", 0, null, []);

    public static ManagerProfileView Of(WorldState world, OrganizationId observer, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!observer.IsAssigned)
        {
            throw new ArgumentException("Observer id is unassigned.", nameof(observer));
        }

        Contract? chosen = null;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != observer
                || !contract.Role.IsStaff
                || contract.Role.StaffRole != StaffRole.TeamPrincipal
                || !contract.IsActiveOn(today))
            {
                continue;
            }

            if (chosen is null || contract.Start > chosen.Start)
            {
                chosen = contract;
            }
        }

        if (chosen is null)
        {
            return None;
        }

        var person = world.GetPerson(chosen.PersonId);
        IReadOnlyList<KnownAttributeView> attributes = world.KnowledgeOf(observer, person.Id) is PersonKnowledgeView belief
            ? belief.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()
            : [];
        var age = today.Year - person.BirthDate.Year;
        if (today.Month < person.BirthDate.Month || (today.Month == person.BirthDate.Month && today.Day < person.BirthDate.Day))
        {
            age--;
        }

        return new ManagerProfileView(true, person.Id.Value, person.Name, person.Nationality, Math.Max(0, age), chosen.Start.ToString(), attributes);
    }
}
