using Paddock.Application.Access;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// One person on the market: a free agent, or a driver or staff member under contract elsewhere. <see cref="Kind"/> is
/// <c>driver</c> or the name of the staff role. Attributes are the bands this team believes, never the hidden number (INV-003);
/// <see cref="Overall"/> is their mean on the 1 to 20 scale. <see cref="ExpectedSalary"/> is what this team believes the person
/// is worth per season (the reference salary), not the pay of the contract they hold, which is another team's business.
/// <see cref="ContractEnd"/> is the end of the contract the person holds, or for a free agent the day they became free.
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
    int Age,
    string Kind = "driver",
    int? Overall = null,
    long ExpectedSalary = 0,
    bool Female = false);

/// <summary>Free agents and people under contract elsewhere, as the observer's team knows them. Drivers and staff are in the same lists.</summary>
public sealed record MarketView(IReadOnlyList<MarketPersonView> FreeAgents, IReadOnlyList<MarketPersonView> Contracted);

/// <summary>The market a manager may read. A pure query (INV-005).</summary>
public static class MarketRead
{
    public static MarketView Of(AccessContext access, ContractBook book, OrganizationId observer, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(book);
        var free = new List<MarketPersonView>();
        var query = new FreeAgentQuery(book);
        foreach (var person in query.List(access, observer, today))
        {
            var subject = person.IsDriver
                ? NegotiationSubject.DriverSeat
                : person.StaffRoles.Where(StaffCatalogue.IsTeamRoster).Select(NegotiationSubject.Staff).Cast<NegotiationSubject?>().FirstOrDefault();
            if (subject is not NegotiationSubject wanted)
            {
                continue;
            }

            var record = book.World.GetPerson(person.Person);
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
                AgeOn(record.BirthDate, today),
                Kind(wanted),
                PeopleViews.Overall(wanted, book.World.KnowledgeOf(observer, person.Person)),
                book.ReferenceSalary(observer, person.Person, wanted, today),
                record.IsFemale));
        }

        var contracted = new List<MarketPersonView>();
        foreach (var contract in book.World.Contracts.OrderBy(item => item.PersonId.Value, StringComparer.Ordinal))
        {
            if (!contract.IsActiveOn(today) || contract.OrganizationId == observer)
            {
                continue;
            }

            if (!contract.Role.IsDriver && !(contract.Role.IsStaff && StaffCatalogue.IsTeamRoster(contract.Role.StaffRole)))
            {
                continue;
            }

            var person = book.World.GetPerson(contract.PersonId);
            if (person.IsRetired)
            {
                continue;
            }

            var subject = ContractEngine.SubjectOf(contract);
            var knowledge = book.World.KnowledgeOf(observer, person.Id);
            IReadOnlyList<KnownAttributeView> attributes = knowledge is { } known
                ? known.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()
                : [];
            var organization = book.World.GetOrganization(contract.OrganizationId);
            // The end of the last contract the person holds: a signed renewal moves it past the one running today.
            var holds = book.LiveContractsOf(person.Id, today);
            contracted.Add(new MarketPersonView(
                person.Id.Value,
                person.Name,
                person.Nationality,
                false,
                contract.OrganizationId.Value,
                organization.NameOn(today),
                contract.Role.IsDriver ? contract.Role.Seat.ToString() : null,
                (holds.Count > 0 ? holds[^1].End : contract.End).ToString(),
                attributes,
                AgeOn(person.BirthDate, today),
                Kind(subject),
                PeopleViews.Overall(subject, knowledge),
                book.ReferenceSalary(observer, person.Id, subject, today),
                person.IsFemale));
        }

        return new MarketView(free, contracted);
    }

    private static string Kind(NegotiationSubject subject) =>
        subject.Kind == NegotiationSubjectKind.DriverSeat ? "driver" : subject.StaffRole.ToString();

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
