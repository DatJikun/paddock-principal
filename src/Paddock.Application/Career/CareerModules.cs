using Paddock.Application.Board;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Principals;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;

namespace Paddock.Application.Career;

/// <summary>
/// The systems every career runs, in the order they attach. A system that joins the career loop adds one line here and
/// brings its own <see cref="ICareerModule"/>; nothing else changes (no edit in <see cref="CareerHost"/>, in
/// <see cref="Commands.CommandCodec"/> or in the day loop; its save store and its migration are added in Paddock.Persistence).
/// A module goes after the modules whose services it reads in Attach (see <see cref="CareerModuleHost"/>): objectives first (the
/// fact registry), then finance, contracts, the pool, cars, sponsors, supply, racing (it offers the next race), development,
/// the board (it needs the contract book), and the AI principals last (they read the books the others provided).
/// The day order is not this list's order: each day handler has its own <see cref="Paddock.Simulation.Time.IDayHandler.Order"/>.
/// <para>
/// Day order today: host season change 5, pool 10, principal seat watch 15, ageing 20, last season 25, contract expiry 30,
/// season rollover 40, race weekend 50, negotiations 700, contract lifecycle 710, sponsors 750, supply 760, development 780,
/// finance 800, objectives 900, board 910 (TECH 6.2).
/// </para>
/// </summary>
public static class CareerModules
{
    public static IReadOnlyList<ICareerModule> Default { get; } = Array.AsReadOnly<ICareerModule>(
    [
        new ObjectivesModule(),
        new FinanceModule(),
        new ContractsModule(),
        new PoolModule(),
        new CarsModule(),
        new SponsorsModule(),
        new SupplyModule(),
        new RacingModule(),
        new DevelopmentModule(),
        new BoardModule(),
        new PrincipalsModule(),
    ]);
}
