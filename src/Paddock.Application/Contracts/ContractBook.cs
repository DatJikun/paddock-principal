using Paddock.Domain.Contracts;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// The live contract state a host holds between days: the world (persons, organizations, contracts, beliefs) and the
/// <see cref="ContractsSection"/> next to it, with the <see cref="ContractEnvironment"/> of ports. Like the inbox book it is a
/// mutable holder around immutable values; commands and the day handlers replace them. Truth lives here: a manager reads it
/// only through <see cref="NegotiationQuery"/> and <see cref="FreeAgentQuery"/> (INV-003).
/// <para>
/// <see cref="World"/> is the world that contract commands change. In a running career the host keeps it the same as the
/// career session's world: hand the session's world in, and take <see cref="World"/> back after a command or a day.
/// <see cref="Into"/> puts the section into a world for hashing and saving; an empty section is left out, so a world that
/// never used contracts keeps its hash.
/// </para>
/// </summary>
public sealed class ContractBook
{
    private WorldState _world;
    private Func<WorldState>? _read;
    private Action<WorldState>? _write;

    public ContractBook(WorldState world, ContractEnvironment environment, ContractsSection? section = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(environment);
        _world = world;
        Environment = environment;
        Section = section ?? world.Section<ContractsSection>(ContractsSection.SectionName) ?? ContractsSection.Empty;
    }

    public ContractEnvironment Environment { get; }

    /// <summary>
    /// True when the book reads and writes the host's own world (<see cref="Bind"/>), so a change a command or handler of
    /// another system made to it is seen at once and a change made here is not lost when the host puts its own world back.
    /// </summary>
    public bool IsLive => _read is not null;

    /// <summary>
    /// The current world (an immutable value). For a live book this is the host's world as it is now, so the board, the finance
    /// ledger and the contracts always work on one world.
    /// </summary>
    public WorldState World => _read is null ? _world : _read();

    /// <summary>
    /// Ties the book to the host's world: <paramref name="read"/> gives the world as it is, <paramref name="write"/> stores one
    /// the book changed. After this <see cref="UseWorld"/> stores to the host instead of keeping a copy. The contracts section
    /// stays the book's own (it is put into the world by <see cref="Into(WorldState)"/>).
    /// </summary>
    public void Bind(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    /// <summary>The current contracts and negotiations (an immutable value).</summary>
    public ContractsSection Section { get; private set; }

    /// <summary>The given world with this book's section in it. An empty section is left out.</summary>
    public WorldState Into(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Section.IsEmpty ? world.WithoutSection(ContractsSection.SectionName) : world.WithSection(Section);
    }

    /// <summary>This book's world with its section in it.</summary>
    public WorldState Into() => Into(World);

    /// <summary>Replaces the world. The host calls this when its own clock changed the world (a new day, a new person).</summary>
    public void UseWorld(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        SetWorld(world);
    }

    internal void Update(WorldState world, ContractsSection section)
    {
        SetWorld(world);
        Section = section;
    }

    internal void Update(ContractsSection section) => Section = section;

    internal void Update(WorldState world) => SetWorld(world);

    private void SetWorld(WorldState world)
    {
        if (_write is null)
        {
            _world = world;
        }
        else
        {
            _write(world);
        }
    }

    /// <summary>Negotiations an organization may run at once: 3 plus its principal's negotiation attribute over 5 (ESTIMATES).</summary>
    public int Capacity(OrganizationId organization) =>
        NegotiationEstimates.ParallelBase
        + (Environment.Principal.Negotiation(World, organization) / NegotiationEstimates.ParallelPerNegotiationPoints);

    /// <summary>The exclusive contracts of a person that have not ended by <paramref name="today"/>, in order of their end.</summary>
    public IReadOnlyList<Contract> LiveContractsOf(PersonId person, GameDate today) =>
        World.Contracts
            .Where(contract => contract.PersonId == person && contract.Exclusive && contract.End >= today)
            .OrderBy(contract => contract.End)
            .ThenBy(contract => contract.Id.Value, StringComparer.Ordinal)
            .ToArray();

    /// <summary>The contract a person holds today, or null.</summary>
    public Contract? ActiveContractOf(PersonId person, GameDate today) =>
        LiveContractsOf(person, today).FirstOrDefault(contract => contract.Start <= today);

    /// <summary>The reference salary for a person: era benchmark pay at the stars the proposer believes the person has.</summary>
    public long ReferenceSalary(OrganizationId proposer, PersonId person, NegotiationSubject subject, GameDate today) =>
        ReferenceOffer.ReferenceSalary(Environment.Pay, today.Season, subject, World.KnowledgeOf(proposer, person));

    /// <summary>A contract written as the terms a person would weigh: a salary of zero ("not modelled") reads as the reference.</summary>
    public OfferTerms AsTerms(Contract contract, long referenceSalary, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var terms = Section.TermsOf(contract.Id);
        var left = Math.Max(1, (int)Math.Ceiling(today.DaysUntil(contract.End) / 365.0));
        var years = Math.Min(left, NegotiationEstimates.MaxYears);
        OfferOption? option = contract.Option is ContractOption held
            ? new OfferOption(terms?.OptionHolder ?? OptionHolder.Team, held.ExtraYears)
            : null;
        return new OfferTerms(
            contract.Salary == 0 ? referenceSalary : contract.Salary,
            terms?.PointsBonus ?? 0,
            terms?.WinBonus ?? 0,
            terms?.TitleBonus ?? 0,
            years,
            contract.Role.IsDriver ? contract.Role.Seat : null,
            option,
            terms?.Exit);
    }

    /// <summary>
    /// What a person weighs when judging an organization's terms today. The reference salary is what the proposer believes
    /// about the person (bands), never the true attributes (INV-003).
    /// </summary>
    public EvaluationContext ContextFor(OrganizationId organization, PersonId person, NegotiationSubject subject, GameDate today, long referenceSalary)
    {
        var record = World.GetPerson(person);
        var current = ActiveContractOf(person, today);
        return new EvaluationContext(
            Environment.Personality.TraitsOf(person),
            today.Year - record.BirthDate.Year,
            AppealOf(organization, today),
            referenceSalary,
            subject,
            current is not null && current.OrganizationId == organization);
    }

    /// <summary>
    /// How attractive an organization looks to a person: its public appeal, with the prestige moved by the reputation of the
    /// principal who runs it (T45): up to about 0.15 either way, nothing at the middle reputation. A pure query (INV-005).
    /// </summary>
    public OrganizationAppeal AppealOf(OrganizationId organization, GameDate today)
    {
        var appeal = Environment.Appeal.Appeal(organization, today);
        var shift = Domain.Board.ReputationModel.PrestigeShift(Environment.Reputation.Reputation(organization, today));
        return shift == 0.0
            ? appeal
            : new OrganizationAppeal(Math.Clamp(appeal.Prestige + shift, 0.0, 1.0), appeal.ExpectedCar, appeal.Risk);
    }

    /// <summary>
    /// The utility of staying on the contract a person holds at another organization, or null when there is none. This is one of
    /// the alternatives an offer has to beat.
    /// </summary>
    public double? CurrentContractUtility(PersonId person, OrganizationId proposer, GameDate today)
    {
        var current = ActiveContractOf(person, today);
        if (current is null || current.OrganizationId == proposer)
        {
            return null;
        }

        return UtilityOfStaying(current, today);
    }

    /// <summary>The utility a person finds in the contract they hold today, judged on today's appeal of the employer.</summary>
    public double UtilityOfStaying(Contract contract, GameDate today)
    {
        var subject = contract.Role.IsDriver ? NegotiationSubject.DriverSeat : NegotiationSubject.Staff(contract.Role.StaffRole);
        var reference = ReferenceSalary(contract.OrganizationId, contract.PersonId, subject, today);
        var context = ContextFor(contract.OrganizationId, contract.PersonId, subject, today, reference);
        var utility = CounterpartyEvaluator.Evaluate(AsTerms(contract, reference, today), context).Utility;
        var bias = World.Section<RaisesSection>(RaisesSection.SectionName)?.LeaveBias(contract.PersonId, contract.OrganizationId) ?? 0;
        return utility - (bias * NegotiationEstimates.RaiseLeaveUtility);
    }
}
