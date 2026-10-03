namespace Paddock.Domain.World;

/// <summary>
/// Issues person, organization, and contract ids and never reuses one (INV-009).
/// There is no free list. Removing an entity leaves the id in <see cref="Issued"/>.
/// Real people and organizations keep their authored strings. Generated ids come from
/// counters that travel with the allocator, which is part of <see cref="WorldState"/>.
/// Manager ids already exist in the command layer (T17) and are not issued here.
/// One string is used for at most one id, across people, organizations, and contracts.
/// </summary>
public sealed class IdAllocator
{
    private readonly string[] _issued;

    public IdAllocator(long nextPerson, long nextOrganization, long nextContract, IReadOnlyList<string> issued)
    {
        if (nextPerson < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPerson), nextPerson, "The next person id must be at least 1.");
        }

        if (nextOrganization < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextOrganization), nextOrganization, "The next organization id must be at least 1.");
        }

        if (nextContract < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextContract), nextContract, "The next contract id must be at least 1.");
        }

        ArgumentNullException.ThrowIfNull(issued);
        var copy = new string[issued.Count];
        for (var i = 0; i < issued.Count; i++)
        {
            copy[i] = RequireCanonical(issued[i]);
        }

        Array.Sort(copy, StringComparer.Ordinal);
        for (var i = 0; i < copy.Length; i++)
        {
            if (i > 0 && string.Equals(copy[i], copy[i - 1], StringComparison.Ordinal))
            {
                throw new ArgumentException("Issued ids contain a duplicate.", nameof(issued));
            }

            EnsureCounterIsPast(copy[i], IdText.PersonPrefix, nextPerson, nameof(nextPerson));
            EnsureCounterIsPast(copy[i], IdText.OrganizationPrefix, nextOrganization, nameof(nextOrganization));
            EnsureCounterIsPast(copy[i], IdText.ContractPrefix, nextContract, nameof(nextContract));
        }

        NextPerson = nextPerson;
        NextOrganization = nextOrganization;
        NextContract = nextContract;
        _issued = copy;
    }

    public static IdAllocator Empty { get; } = new(1, 1, 1, []);

    public long NextPerson { get; }

    public long NextOrganization { get; }

    public long NextContract { get; }

    public IReadOnlyList<string> Issued => _issued;

    public bool WasIssued(string canonical) =>
        Array.BinarySearch(_issued, canonical, StringComparer.Ordinal) >= 0;

    public (IdAllocator Allocator, PersonId Id) AllocatePerson()
    {
        EnsureRoom(NextPerson);
        var id = PersonId.Generated(NextPerson);
        return (Issue(id.Value, NextPerson + 1, NextOrganization, NextContract), id);
    }

    public (IdAllocator Allocator, PersonId Id) AdoptPerson(string authoredId)
    {
        var id = PersonId.Real(authoredId);
        return (Issue(id.Value, NextPerson, NextOrganization, NextContract), id);
    }

    public (IdAllocator Allocator, OrganizationId Id) AllocateOrganization()
    {
        EnsureRoom(NextOrganization);
        var id = OrganizationId.Generated(NextOrganization);
        return (Issue(id.Value, NextPerson, NextOrganization + 1, NextContract), id);
    }

    public (IdAllocator Allocator, OrganizationId Id) AdoptOrganization(string authoredId)
    {
        var id = OrganizationId.Real(authoredId);
        return (Issue(id.Value, NextPerson, NextOrganization, NextContract), id);
    }

    public (IdAllocator Allocator, ContractId Id) AllocateContract()
    {
        EnsureRoom(NextContract);
        var id = ContractId.Generated(NextContract);
        return (Issue(id.Value, NextPerson, NextOrganization, NextContract + 1), id);
    }

    private IdAllocator Issue(string canonical, long nextPerson, long nextOrganization, long nextContract)
    {
        if (WasIssued(canonical))
        {
            throw new InvalidOperationException($"Id '{canonical}' was already issued and cannot be reused.");
        }

        var issued = new string[_issued.Length + 1];
        var placed = false;
        var destination = 0;
        foreach (var existing in _issued)
        {
            if (!placed && string.CompareOrdinal(canonical, existing) < 0)
            {
                issued[destination++] = canonical;
                placed = true;
            }

            issued[destination++] = existing;
        }

        if (!placed)
        {
            issued[destination] = canonical;
        }

        return new IdAllocator(nextPerson, nextOrganization, nextContract, issued);
    }

    private static void EnsureRoom(long next)
    {
        if (next == long.MaxValue)
        {
            throw new InvalidOperationException("The stable id counter is exhausted.");
        }
    }

    private static string RequireCanonical(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Issued ids cannot be empty.", nameof(value));
        }

        if (IdText.IsGenerated(value))
        {
            return value;
        }

        return IdText.RequireReal(value, nameof(value));
    }

    private static void EnsureCounterIsPast(string canonical, string prefix, long next, string paramName)
    {
        if (IdText.TryReadSequence(canonical, prefix, out var sequence) && sequence >= next)
        {
            throw new ArgumentOutOfRangeException(paramName, next, "The counter must be past every issued id.");
        }
    }
}
