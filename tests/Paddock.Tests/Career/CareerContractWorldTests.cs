using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;

namespace Paddock.Tests.Career;

/// <summary>
/// #160: the contract book and the session share one world. A contract that a command signs (the AI accepts a counter, a
/// renewal is agreed) must be in the world the next day, not only in the negotiation that says it was signed. Before the fix the
/// morning put the session's world back over the book's, so Chaos seed 7 lost the contracts commands had signed, and the
/// world then issued the same contract id again to someone else.
/// </summary>
public sealed class CareerContractWorldTests
{
    [Fact]
    public void EveryContractANegotiationSignedIsInTheWorldForTheSamePersonAndTeam()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1950, 7);
        CareerHost.Run(session, 1953);

        var section = session.World.Section<ContractsSection>(ContractsSection.SectionName);
        Assert.NotNull(section);
        var signed = section!.Negotiations.Where(negotiation => negotiation.SignedContract is not null).ToArray();
        Assert.NotEmpty(signed);
        var byId = session.World.Contracts.ToDictionary(contract => contract.Id.Value, StringComparer.Ordinal);
        foreach (var negotiation in signed)
        {
            Assert.True(byId.TryGetValue(negotiation.SignedContract!.Value.Value, out var contract), negotiation.Id + " signed a contract that is not in the world.");
            Assert.Equal(negotiation.Counterparty, contract!.PersonId);
            Assert.Equal(negotiation.Proposer, contract.OrganizationId);
        }
    }
}
