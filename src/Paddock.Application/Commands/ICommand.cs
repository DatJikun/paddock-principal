using Paddock.Application.Managers;

namespace Paddock.Application.Commands;

/// <summary>
/// An order from one manager. <see cref="CommandQueue"/> assigns <see cref="SubmissionNumber"/>
/// (monotonic, never reused). <see cref="IssuedOn"/> is the game date the order was issued on.
/// Stored as <see cref="DateOnly"/> until T16's <c>GameDate</c> replaces it.
/// </summary>
public interface ICommand
{
    ManagerId ManagerId { get; }

    long SubmissionNumber { get; }

    DateOnly IssuedOn { get; }

    ICommand WithSubmissionNumber(long submissionNumber);
}
