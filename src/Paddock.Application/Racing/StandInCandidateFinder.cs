using Paddock.Application.Principals;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Racing;

/// <summary>
/// Finds and ranks stand-in driver candidates for a car when its regular driver is injured (PP-061, #219).
/// Order of evaluation:
/// 1. The team's contracted test/reserve driver (SeatStatus.Reserve).
/// 2. Free agents or talent pool drivers, sorted by the team's own knowledge (never hidden truth).
/// </summary>
public static class StandInCandidateFinder
{
    public static IReadOnlyList<Person> FindCandidates(
        WorldState world,
        OrganizationId teamId,
        GameDate onDate,
        IReadOnlySet<string>? excludeDriverIds = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var candidates = new List<Person>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        if (excludeDriverIds is not null)
        {
            seenIds.UnionWith(excludeDriverIds);
        }

        // 1. Contracted reserve driver
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId == teamId
                && contract.IsActiveOn(onDate)
                && contract.Role.IsDriver
                && contract.Role.Seat == SeatStatus.Reserve)
            {
                var person = world.GetPerson(contract.PersonId);
                if (person is not null && !person.IsRetired && !person.IsInjured(onDate) && seenIds.Add(person.Id.Value))
                {
                    candidates.Add(person);
                    break;
                }
            }
        }

        // 2. Free agents and talent pool
        var marketCandidates = new List<Person>();

        // Free agents
        foreach (var person in world.Persons)
        {
            if (person.Roles.Contains(PersonRole.Driver)
                && !person.IsRetired
                && !person.IsInjured(onDate)
                && !seenIds.Contains(person.Id.Value)
                && !world.Contracts.Any(c => c.PersonId == person.Id && c.IsActiveOn(onDate)))
            {
                marketCandidates.Add(person);
                seenIds.Add(person.Id.Value);
            }
        }

        // Talent pool
        var pool = world.Section<TalentPoolSection>(TalentPoolSection.SectionName);
        if (pool is not null)
        {
            foreach (var member in pool.Members)
            {
                var person = world.GetPerson(member.Id);
                if (person is not null
                    && !person.IsRetired
                    && !person.IsInjured(onDate)
                    && !seenIds.Contains(person.Id.Value)
                    && !world.Contracts.Any(c => c.PersonId == person.Id && c.IsActiveOn(onDate)))
                {
                    marketCandidates.Add(person);
                    seenIds.Add(person.Id.Value);
                }
            }
        }

        // Sort market candidates by team's belief (never hidden truth)
        var rankedMarket = marketCandidates
            .Select(p =>
            {
                var belief = world.KnowledgeOf(teamId, p.Id);
                var score = TeamKnowledge.Quality(belief, GenerationEstimates.DriverAttributeKeys).Mid;
                return (Person: p, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Person.Id.Value, StringComparer.Ordinal)
            .Select(x => x.Person);

        candidates.AddRange(rankedMarket);
        return candidates;
    }
}
