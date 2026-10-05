using Paddock.Application.Board;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;

namespace Paddock.Application.Career;

/// <summary>
/// The systems every career runs, in the order they attach. A system that joins the career loop adds one line here and
/// brings its own <see cref="ICareerModule"/>; nothing else changes (no edit in <see cref="CareerHost"/>, in
/// <see cref="Commands.CommandCodec"/> or in the day loop; its save store and its migration are added in Paddock.Persistence).
/// A module goes after the modules whose services it reads in Attach (see <see cref="CareerModuleHost"/>): objectives first (the
/// fact registry), then finance, contracts, the pool, cars, sponsors, and the board last (it needs the contract book).
/// The day order is not this list's order: each day handler has its own <see cref="Paddock.Simulation.Time.IDayHandler.Order"/>.
/// <para>
/// Day order today: pool 10, ageing 20, last season 25, contract expiry 30, season rollover 40, negotiations 700, contract
/// lifecycle 710, sponsors 750, finance 800, objectives 900, board 910 (TECH 6.2). T42 development, T43 suppliers and T44 the AI
/// principals add their lines below.
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
        new BoardModule(),
    ]);
}
