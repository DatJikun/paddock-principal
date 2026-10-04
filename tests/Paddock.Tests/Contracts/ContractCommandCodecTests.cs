using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Domain.Codec;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.World;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>The six contract commands survive a save of the command log (issue #123 codec). Fixtures are SYNTHETIC.</summary>
public class ContractCommandCodecTests
{
    private static readonly DateOnly Day = new(1955, 6, 1);

    [Fact]
    public void EveryContractCommandRoundTripsWithItsManagerDateAndNumber()
    {
        var full = Terms(130_000, SeatStatus.NumberOne, 3, 1_000, 20_000, 100_000, new OfferOption(OptionHolder.Person, 2), new ExitClause(5));
        ICommand[] commands =
        [
            new OpenNegotiationCommand { ManagerId = Anna, IssuedOn = Day, Organization = TeamA, Person = DriverX, Subject = NegotiationSubject.DriverSeat, SubmissionNumber = 1 },
            new OpenNegotiationCommand { ManagerId = Anna, IssuedOn = Day, Organization = OrganizationId.Generated(3), Person = PersonId.Generated(9), Subject = NegotiationSubject.Staff(StaffRole.Strategist), Deadline = Day.AddDays(20), SubmissionNumber = 2 },
            new SubmitOfferCommand { ManagerId = Anna, IssuedOn = Day, NegotiationId = "neg:1", Terms = full, SubmissionNumber = 3 },
            new SubmitOfferCommand { ManagerId = Anna, IssuedOn = Day, NegotiationId = "neg:1", Terms = Terms(seat: null), SubmissionNumber = 4 },
            new AcceptCounterOfferCommand { ManagerId = Bram, IssuedOn = Day, NegotiationId = "neg:2", SubmissionNumber = 5 },
            new WalkAwayCommand { ManagerId = Bram, IssuedOn = Day, NegotiationId = "neg:2", SubmissionNumber = 6 },
            new RenewContractCommand { ManagerId = Bram, IssuedOn = Day, Contract = ContractId.Generated(4), ExerciseOption = true, SubmissionNumber = 7 },
            new RenewContractCommand { ManagerId = Bram, IssuedOn = Day, Contract = ContractId.Generated(4), Offer = full, Deadline = Day.AddDays(30), SubmissionNumber = 8 },
            new TerminateContractCommand { ManagerId = Bram, IssuedOn = Day, Contract = ContractId.Generated(4), Compensation = 12_345, SubmissionNumber = 9 },
        ];

        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
            Assert.Equal(command.GetType(), decoded.GetType());
        }
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1,0,0,0,0,-,-,-")]
    [InlineData("x,0,0,0,1,Equal,-,-")]
    [InlineData("1,0,0,0,1,Boss,-,-")]
    public void MalformedTermsInASaveFailLoudly(string terms)
    {
        Assert.Throws<InvalidDataException>(() => ContractCommandCodecs.ParseTerms(terms));
    }
}
