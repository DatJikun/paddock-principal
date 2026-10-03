using Paddock.Domain.Time;

namespace Paddock.Domain.World;

public sealed partial class WorldState
{
    /// <summary>
    /// Rebuilds a state from stored parts, for example when a save is loaded. The parts are checked the way
    /// the editing methods would have checked them: every id was issued, references resolve, no person holds two
    /// overlapping exclusive contracts, lineage links are reciprocal and form chains. Nothing is allocated, so the
    /// <paramref name="ids"/> counters and issued list come back exactly as saved (INV-009).
    /// </summary>
    public static WorldState Restore(
        GameDate currentDate,
        IdAllocator ids,
        IEnumerable<Person> persons,
        IEnumerable<Organization> organizations,
        IEnumerable<Contract> contracts,
        IEnumerable<PersonKnowledge> knowledge)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(knowledge);

        var personMap = new SortedDictionary<string, Person>(StringComparer.Ordinal);
        foreach (var person in persons)
        {
            ArgumentNullException.ThrowIfNull(person);
            RequireIssued(ids, person.Id.Value);
            if (!personMap.TryAdd(person.Id.Value, person))
            {
                throw new InvalidOperationException($"Person '{person.Id}' appears twice.");
            }
        }

        var organizationMap = new SortedDictionary<string, Organization>(StringComparer.Ordinal);
        foreach (var organization in organizations)
        {
            ArgumentNullException.ThrowIfNull(organization);
            RequireIssued(ids, organization.Id.Value);
            if (!organizationMap.TryAdd(organization.Id.Value, organization))
            {
                throw new InvalidOperationException($"Organization '{organization.Id}' appears twice.");
            }
        }

        var contractMap = new SortedDictionary<string, Contract>(StringComparer.Ordinal);
        var exclusiveByPerson = new Dictionary<string, List<Contract>>(StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            ArgumentNullException.ThrowIfNull(contract);
            RequireIssued(ids, contract.Id.Value);
            if (!personMap.TryGetValue(contract.PersonId.Value, out var holder))
            {
                throw new InvalidOperationException($"Contract '{contract.Id}' names unknown person '{contract.PersonId}'.");
            }

            if (holder.IsRetired)
            {
                throw new InvalidOperationException($"Contract '{contract.Id}' belongs to retired person '{contract.PersonId}'.");
            }

            if (!organizationMap.ContainsKey(contract.OrganizationId.Value))
            {
                throw new InvalidOperationException($"Contract '{contract.Id}' names unknown organization '{contract.OrganizationId}'.");
            }

            if (!contractMap.TryAdd(contract.Id.Value, contract))
            {
                throw new InvalidOperationException($"Contract '{contract.Id}' appears twice.");
            }

            if (contract.Exclusive)
            {
                if (!exclusiveByPerson.TryGetValue(contract.PersonId.Value, out var list))
                {
                    list = [];
                    exclusiveByPerson.Add(contract.PersonId.Value, list);
                }

                list.Add(contract);
            }
        }

        foreach (var list in exclusiveByPerson.Values)
        {
            list.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            var latestEnd = list[0].End;
            for (var i = 1; i < list.Count; i++)
            {
                if (list[i].Start <= latestEnd)
                {
                    throw new InvalidOperationException(
                        $"Person '{list[i].PersonId}' has overlapping exclusive contracts including '{list[i].Id}'.");
                }

                if (list[i].End > latestEnd)
                {
                    latestEnd = list[i].End;
                }
            }
        }

        RequireConsistentLineage(organizationMap);

        var knowledgeMap = new SortedDictionary<string, PersonKnowledge>(StringComparer.Ordinal);
        foreach (var belief in knowledge)
        {
            ArgumentNullException.ThrowIfNull(belief);
            if (!organizationMap.ContainsKey(belief.ObserverId.Value))
            {
                throw new InvalidOperationException($"Belief names unknown organization '{belief.ObserverId}'.");
            }

            if (!personMap.ContainsKey(belief.SubjectId.Value))
            {
                throw new InvalidOperationException($"Belief names unknown person '{belief.SubjectId}'.");
            }

            if (!knowledgeMap.TryAdd(KnowledgeKey(belief.ObserverId, belief.SubjectId), belief))
            {
                throw new InvalidOperationException(
                    $"Organization '{belief.ObserverId}' has two beliefs about '{belief.SubjectId}'.");
            }
        }

        return new WorldState(currentDate, ids, personMap, organizationMap, contractMap, knowledgeMap);
    }

    private static void RequireIssued(IdAllocator ids, string canonical)
    {
        if (!ids.WasIssued(canonical))
        {
            throw new InvalidOperationException($"Id '{canonical}' is in use but was never issued.");
        }
    }

    private static void RequireConsistentLineage(SortedDictionary<string, Organization> organizations)
    {
        foreach (var organization in organizations.Values)
        {
            foreach (var link in organization.Lineage)
            {
                if (!organizations.TryGetValue(link.OtherId.Value, out var other))
                {
                    throw new InvalidOperationException(
                        $"Organization '{organization.Id}' links to unknown organization '{link.OtherId}'.");
                }

                var reverse = link.Direction == LineageDirection.Predecessor ? LineageDirection.Successor : LineageDirection.Predecessor;
                var mirrored = false;
                foreach (var back in other.Lineage)
                {
                    if (back.Direction == reverse && back.OtherId == organization.Id && back.From == link.From && back.To == link.To)
                    {
                        mirrored = true;
                        break;
                    }
                }

                if (!mirrored)
                {
                    throw new InvalidOperationException(
                        $"Lineage link from '{organization.Id}' to '{link.OtherId}' has no matching link on the other side.");
                }
            }
        }

        // Every chain starts at an organization with no predecessor, so an organization that no start reaches
        // sits on a cycle.
        var reached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var organization in organizations.Values)
        {
            if (One(organization, LineageDirection.Predecessor) is not null)
            {
                continue;
            }

            var current = organization;
            while (reached.Add(current.Id.Value))
            {
                if (One(current, LineageDirection.Successor) is not OrganizationId next)
                {
                    break;
                }

                current = organizations[next.Value];
            }
        }

        if (reached.Count != organizations.Count)
        {
            throw new InvalidOperationException("The lineage links form a cycle.");
        }
    }
}
