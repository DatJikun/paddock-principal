using Paddock.Application.Access;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// One driver on the market: a free agent, or a driver under contract elsewhere.
/// Attributes are the bands this team believes, never the hidden number (INV-003).
/// </summary>
public sealed record MarketPersonView(
    string PersonId,
    string Name,
    string Nationality,
    bool FreeAgent,
    string? OrganizationId,
    string? OrganizationName,
    string? Seat,
    string? ContractEnd,
    IReadOnlyList<KnownAttributeView> Attributes,
    int Age);

/// <summary>Free agents and contracted drivers, as the observer's team knows them.</summary>
public sealed record MarketView(IReadOnlyList<MarketPersonView> FreeAgents, IReadOnlyList<MarketPersonView> Contracted);

/// <summary>The driver market a manager may read. A pure query (INV-005).</summary>
public static class MarketRead
{
    public static MarketView Of(AccessContext access, ContractBook book, OrganizationId observer, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(book);
        var free = new List<MarketPersonView>();
        foreach (var person in new FreeAgentQuery(book).List(access, observer, today, Paddock.Domain.Contracts.NegotiationSubject.DriverSeat))
        {
            if (!person.IsDriver)
            {
                continue;
            }

            free.Add(new MarketPersonView(
                person.Person.Value,
                person.Name,
                person.Nationality,
                true,
                null,
                null,
                null,
                person.FreeSince?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                person.KnownAttributes,
                AgeOn(book.World.GetPerson(person.Person).BirthDate, today)));
        }

        var contracted = new List<MarketPersonView>();
        foreach (var contract in book.World.Contracts.OrderBy(item => item.PersonId.Value, StringComparer.Ordinal))
        {
            if (!contract.Role.IsDriver || !contract.IsActiveOn(today) || contract.OrganizationId == observer)
            {
                continue;
            }

            var person = book.World.GetPerson(contract.PersonId);
            if (person.IsRetired)
            {
                continue;
            }

            IReadOnlyList<KnownAttributeView> attributes = book.World.KnowledgeOf(observer, person.Id) is { } knowledge
                ? knowledge.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()
                : [];
            var organization = book.World.GetOrganization(contract.OrganizationId);
            contracted.Add(new MarketPersonView(
                person.Id.Value,
                person.Name,
                person.Nationality,
                false,
                contract.OrganizationId.Value,
                organization.NameOn(today),
                contract.Role.Seat.ToString(),
                contract.End.ToString(),
                attributes,
                AgeOn(person.BirthDate, today)));
        }

        return new MarketView(free, contracted);
    }

    private static int AgeOn(GameDate born, GameDate on)
    {
        var age = on.Year - born.Year;
        if (on.Month < born.Month || (on.Month == born.Month && on.Day < born.Day))
        {
            age--;
        }

        return Math.Max(0, age);
    }
}
