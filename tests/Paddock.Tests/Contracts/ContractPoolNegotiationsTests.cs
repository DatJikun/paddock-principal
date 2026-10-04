using Paddock.Application.Contracts;
using Paddock.Domain.Contracts;
using Paddock.Domain.Pool;
using Paddock.Domain.World;

namespace Paddock.Tests.Contracts;

public class ContractPoolNegotiationsTests
{
    [Fact]
    public void SigningAPoolDriverOpensANegotiationAndPutsAnOfferOnTheTable()
    {
        var lab = new ContractKit.Lab();
        var port = new ContractPoolNegotiations(lab.Engine);

        Assert.Null(port.ValidateStart(ContractKit.TeamA, ContractKit.DriverX, PoolSigningRole.Test, ContractKit.Start));

        var events = port.Start(ContractKit.Anna, ContractKit.TeamA, ContractKit.DriverX, PoolSigningRole.Junior, ContractKit.Start);

        Assert.Contains(events, item => item is NegotiationOpened);
        var negotiation = Assert.Single(lab.Book.Section.Active());
        Assert.Equal(NegotiationSubject.DriverSeat, negotiation.Subject);
        Assert.Equal(SeatStatus.NumberTwo, negotiation.CurrentOffer?.Seat);
        Assert.Equal(ContractPoolNegotiations.OfferYears, negotiation.CurrentOffer?.Years);
    }
}
