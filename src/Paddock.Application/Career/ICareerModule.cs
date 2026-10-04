using Paddock.Application.Commands;

namespace Paddock.Application.Career;

/// <summary>
/// One system of the career loop (contracts, finance, sponsors, cars, board, and later development, suppliers and the AI
/// principals). A module declares everything the host needs from it in one place, so adding a system to a career is one line in
/// <see cref="CareerModules.Default"/> and no edit to <see cref="CareerHost"/>, <see cref="CommandCodec"/> or the day loop (#160).
/// <para>
/// A module is stateless: it is shared by every career run in the process. Whatever it builds for one run (a book, an engine, a
/// port) it keeps in the <see cref="CareerModuleContext"/> through <see cref="CareerModuleContext.Provide{T}"/>.
/// </para>
/// <para>
/// The host calls every module's <see cref="Configure"/> first, in list order, and then every module's <see cref="Attach"/>.
/// <see cref="Configure"/> is where a module offers ports and facts that another module may read while attaching (finance offers
/// the payroll, the board offers the reputation); <see cref="Attach"/> is where it registers its command handlers and day
/// handlers. Neither may touch the world: opening a new career is <see cref="Open"/>, and a module that has nothing to say there
/// leaves it empty.
/// </para>
/// </summary>
public interface ICareerModule
{
    /// <summary>A stable lowerCamelCase name, used in tests and diagnostics. Two modules of one list may not share it.</summary>
    string Name { get; }

    /// <summary>
    /// The names of the world sections this module owns. A test checks that each has a save store, so a module cannot be added
    /// without being saved (TECH 6.2).
    /// </summary>
    IReadOnlyList<string> Sections { get; }

    /// <summary>The save entries of the commands this module adds. <see cref="CommandCodec.Production"/> is built from them.</summary>
    IReadOnlyList<CommandCodecEntry> CommandCodecs { get; }

    /// <summary>Offers ports, facts and shared services that other modules read in <see cref="Attach"/>.</summary>
    void Configure(CareerModuleContext context);

    /// <summary>Registers command handlers, day handlers, world books and morning hooks.</summary>
    void Attach(CareerModuleContext context);

    /// <summary>
    /// Called once when a run starts, after every module has attached, for a new career and for a resumed one alike. It sets up
    /// what the world lacks (the books of a team that has none, the board of a team that has none) and leaves alone what a saved
    /// world already has, so a resumed career is not changed. It does the work once, directly, because it is the world being set
    /// up, not a manager acting; anything a manager or the AI does later is a command. The world is changed through
    /// <see cref="CareerModuleContext.Session"/> or the module's own book.
    /// </summary>
    void Open(CareerModuleContext context);
}

/// <summary>A module with nothing to say by default: override what the system needs.</summary>
public abstract class CareerModule : ICareerModule
{
    public abstract string Name { get; }

    public virtual IReadOnlyList<string> Sections => [];

    public virtual IReadOnlyList<CommandCodecEntry> CommandCodecs => [];

    public virtual void Configure(CareerModuleContext context)
    {
    }

    public virtual void Attach(CareerModuleContext context)
    {
    }

    public virtual void Open(CareerModuleContext context)
    {
    }
}
