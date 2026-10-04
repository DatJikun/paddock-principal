using Paddock.Domain.Time;

namespace Paddock.Domain.World;

public sealed partial class WorldState
{
    /// <summary>
    /// The exclusive contract that would overlap a new exclusive contract of <paramref name="person"/> running from
    /// <paramref name="start"/> to <paramref name="end"/> (T15 overlap rule), or null when the person is free for the whole span.
    /// </summary>
    public Contract? FindExclusiveOverlap(PersonId person, GameDate start, GameDate end)
    {
        if (!person.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(person));
        }

        if (end < start)
        {
            throw new ArgumentException("A contract ends before it starts.", nameof(end));
        }

        foreach (var existing in _contracts.Values)
        {
            if (existing.PersonId == person && existing.Exclusive && existing.Start <= end && start <= existing.End)
            {
                return existing;
            }
        }

        return null;
    }

    /// <summary>
    /// Gives a contract a new end date and a new option, keeping its id and everything else. Used to extend a contract (the
    /// option is then spent, so pass null) and to end one early (the new end is today). The T15 overlap rule still holds.
    /// </summary>
    public WorldState WithContractTerm(ContractId id, GameDate newEnd, ContractOption? newOption)
    {
        var contract = RequireContract(id);
        if (newEnd < contract.Start)
        {
            throw new ArgumentException("A contract cannot end before it starts.", nameof(newEnd));
        }

        var changed = new Contract(
            contract.Id,
            contract.PersonId,
            contract.OrganizationId,
            contract.Role,
            contract.Start,
            newEnd,
            contract.Salary,
            contract.Exclusive,
            newOption,
            contract.ReleaseClause);
        foreach (var existing in _contracts.Values)
        {
            if (changed.ExclusivelyOverlaps(existing))
            {
                throw new InvalidOperationException(
                    $"Person '{contract.PersonId}' already has exclusive contract '{existing.Id}' overlapping the changed one.");
            }
        }

        var contracts = Clone(_contracts);
        contracts[id.Value] = changed;
        return new WorldState(CurrentDate, Ids, _persons, _organizations, contracts, _knowledge, _sections);
    }
}
