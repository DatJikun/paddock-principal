using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Application.Commands;

public sealed class CommandContext
{
    private readonly InboxBook? _inbox;
    private readonly ContractBook? _contracts;

    public CommandContext(IWorldState world, ManagerRegistry managers, InboxBook? inbox = null, ContractBook? contracts = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(managers);
        World = world;
        Managers = managers;
        _inbox = inbox;
        _contracts = contracts;
    }

    public IWorldState World { get; }

    public ManagerRegistry Managers { get; }

    /// <summary>The inboxes of this game. Throws when the host did not provide one.</summary>
    public InboxBook Inbox => _inbox
        ?? throw new InvalidOperationException("This command context has no inbox. Pass an InboxBook when creating it.");

    /// <summary>The contracts and negotiations of this game. Throws when the host did not provide a contract book.</summary>
    public ContractBook Contracts => _contracts
        ?? throw new InvalidOperationException("This command context has no contract book. Pass a ContractBook when creating it.");
}
