using Paddock.Application.Contracts;
using Paddock.Application.Pool;

namespace Paddock.Application.Career;

/// <summary>
/// The systems every career runs, in the order they attach. A system that joins the career loop adds one line here and
/// brings its own <see cref="ICareerModule"/>; nothing else changes (no edit in <see cref="CareerHost"/>, in
/// <see cref="Commands.CommandCodec"/> or in the day loop). A module goes after the modules whose services it reads in Attach
/// (see <see cref="CareerModuleHost"/>). The day order is not this list's order: each day handler has its own
/// <see cref="Paddock.Simulation.Time.IDayHandler.Order"/>.
/// </summary>
public static class CareerModules
{
    public static IReadOnlyList<ICareerModule> Default { get; } = Array.AsReadOnly<ICareerModule>(
    [
        new ContractsModule(),
        new PoolModule(),
    ]);
}
