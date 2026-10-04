using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.Pool;
using Paddock.Domain.Pool;

namespace Paddock.Tests.Pool;

/// <summary>The pool commands in the command log of a save. Managers and handles are synthetic.</summary>
public class PoolCodecTests
{
    private static readonly DateOnly Day = new(1955, 6, 1);

    [Fact]
    public void EveryPoolCommandRoundTripsWithItsManagerDateAndNumber()
    {
        var manager = new ManagerId("human:1");
        ICommand[] commands =
        [
            new AssignScoutFocusCommand { ManagerId = manager, IssuedOn = Day, PersonHandle = "talent-4", SubmissionNumber = 1 },
            new AssignScoutFocusCommand { ManagerId = manager, IssuedOn = Day, PersonHandle = null, SubmissionNumber = 2 },
            new FundJuniorCommand { ManagerId = manager, IssuedOn = Day, PersonHandle = "talent-9", Programme = JuniorProgramme.ExpensiveFast, SubmissionNumber = 3 },
            new SignPoolDriverCommand { ManagerId = manager, IssuedOn = Day, PersonHandle = "talent-2", Role = PoolSigningRole.Test, SubmissionNumber = 4 },
        ];

        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }

        Assert.Equal("pool.fundJunior/1", CommandCodec.Production.Encode(commands[2]).Tag);
    }

    [Fact]
    public void AnUnknownProgrammeInTheLogIsInvalidData()
    {
        Assert.ThrowsAny<Exception>(() => CommandCodec.Production.Decode(
            "pool.fundJunior/1",
            """{"person":"talent-1","programme":"Free"}""",
            new ManagerId("human:1"),
            1,
            Day));
    }
}
