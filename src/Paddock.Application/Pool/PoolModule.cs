using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Objectives;
using Paddock.Domain.Pool;

namespace Paddock.Application.Pool;

/// <summary>
/// T40 talent pool commands in the career loop. The pool's own day handler (order 10) lives in the session, because it is what
/// keeps the pool full. This module registers the commands a manager gives: signing a pool driver opens a real contract
/// negotiation through the contract engine, so it is listed after <see cref="ContractsModule"/>. Its save entries stay with the
/// other core commands in <see cref="Paddock.Application.Commands.CommandCodec"/>.
/// </summary>
public sealed class PoolModule : CareerModule
{
    public const string ModuleName = "pool";

    public override string Name => ModuleName;

    public override IReadOnlyList<string> Sections => [TalentPoolSection.SectionName];

    public override void Attach(CareerModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddCommandHandler(new SignPoolDriverHandler(
            PoolBook.ForSession(context.Session),
            context.Require<IManagerOrganizations>(),
            new ContractPoolNegotiations(context.Require<ContractEngine>())));
    }
}
