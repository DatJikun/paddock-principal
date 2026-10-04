using Paddock.Application.Board;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Application.Commands;

public sealed class CommandContext
{
    private readonly InboxBook? _inbox;
    private readonly ContractBook? _contracts;
    private readonly BoardBook? _board;

    public CommandContext(IWorldState world, ManagerRegistry managers, InboxBook? inbox = null, ContractBook? contracts = null, BoardBook? board = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(managers);
        World = world;
        Managers = managers;
        _inbox = inbox;
        _contracts = contracts;
        _board = board;
    }

    public IWorldState World { get; }

    public ManagerRegistry Managers { get; }

    /// <summary>The inboxes of this game. Throws when the host did not provide one.</summary>
    public InboxBook Inbox => _inbox
        ?? throw new InvalidOperationException("This command context has no inbox. Pass an InboxBook when creating it.");

    /// <summary>The contracts and negotiations of this game. Throws when the host did not provide a contract book.</summary>
    public ContractBook Contracts => _contracts
        ?? throw new InvalidOperationException("This command context has no contract book. Pass a ContractBook when creating it.");

    /// <summary>The boards, reputations and job search of this game. Throws when the host did not provide a board book.</summary>
    public BoardBook Board => _board
        ?? throw new InvalidOperationException("This command context has no board book. Pass a BoardBook when creating it.");
}
