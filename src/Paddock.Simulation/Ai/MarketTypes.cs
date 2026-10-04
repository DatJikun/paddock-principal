namespace Paddock.Simulation.Ai;

/// <summary>
/// A person as a team believes him: the stars range of his quality (drivers: the mean of the driver attributes; staff: the mean of the
/// attributes of the role), the range of his potential, and his age. Never the true values (INV-003).
/// </summary>
public sealed record PersonView(string Id, int Age, Believed Quality, Believed Potential);

/// <summary>The terms the AI puts on the table. Salary is a yearly amount in whole dollars. <see cref="Seat"/> is null for key staff.</summary>
public sealed record AiOffer(long SalaryDollars, int Years, string? Seat);

/// <summary>The subjects of a market talk: <c>driver</c> or <c>staff:Role</c> (the same text the contract system uses).</summary>
public static class MarketSubjects
{
    public const string Driver = "driver";

    public static bool IsDriver(string subject) => string.Equals(subject, Driver, StringComparison.Ordinal);
}

/// <summary>The seat statuses a driver offer can carry (the contract system's names).</summary>
public static class AiSeats
{
    public const string NumberOne = "NumberOne";

    public const string Equal = "Equal";

    public const string NumberTwo = "NumberTwo";

    public const string Reserve = "Reserve";
}

/// <summary>One of the team's own people under contract, with the terms of the contract and the market ask for him.</summary>
/// <param name="ContractId">The contract.</param>
/// <param name="Person">The person as the team believes him.</param>
/// <param name="Subject">What he does for the team.</param>
/// <param name="Seat">His seat status (drivers) or null.</param>
/// <param name="End">The last day of the contract.</param>
/// <param name="SalaryDollars">His yearly pay now.</param>
/// <param name="AskDollars">What the market would ask for him today (reference pay at the stars the team believes he has).</param>
/// <param name="RenewalOpen">True when the team already has a talk with him about staying.</param>
/// <param name="RenewedAlready">True when he already holds a later contract with the team.</param>
public sealed record Incumbent(
    string ContractId,
    PersonView Person,
    string Subject,
    string? Seat,
    DateOnly End,
    long SalaryDollars,
    long AskDollars,
    bool RenewalOpen,
    bool RenewedAlready);

/// <summary>A person with no contract the team could approach, and what he would ask.</summary>
public sealed record Candidate(PersonView Person, string Subject, long AskDollars);

/// <summary>Where one of the team's talks stands.</summary>
public enum TalkState
{
    /// <summary>Opened, no offer yet: the team has to name its terms.</summary>
    NeedsOffer = 0,

    /// <summary>An offer is with the person.</summary>
    Awaiting = 1,

    /// <summary>The person holds to other terms.</summary>
    Countered = 2,

    /// <summary>The person would sign the offer as it is.</summary>
    PersonAgreed = 3,

    /// <summary>The person is waiting for rival offers.</summary>
    Considering = 4,
}

/// <summary>One of the team's open talks, as its manager reads it (the person's hidden traits are not in it).</summary>
/// <param name="NegotiationId">The negotiation.</param>
/// <param name="Person">The person as the team believes him.</param>
/// <param name="Subject">What the talk is about.</param>
/// <param name="State">Where it stands.</param>
/// <param name="OfferSalary">The team's own offer on the table, or 0 when there is none.</param>
/// <param name="OfferYears">Its length.</param>
/// <param name="CounterSalary">The terms the person holds to (salary), or 0.</param>
/// <param name="CounterYears">Their length.</param>
/// <param name="CounterSeat">The seat in them, or null.</param>
/// <param name="RoundsLeft">How many offers the team may still make.</param>
/// <param name="Deadline">The last day of the talk.</param>
/// <param name="IsRenewal">True when it is about keeping a person the team already employs.</param>
/// <param name="AskDollars">The market ask for the person.</param>
/// <param name="CurrentSalary">The pay of the contract being renewed, or 0.</param>
public sealed record Talk(
    string NegotiationId,
    PersonView Person,
    string Subject,
    TalkState State,
    long OfferSalary,
    int OfferYears,
    long CounterSalary,
    int CounterYears,
    string? CounterSeat,
    int RoundsLeft,
    DateOnly Deadline,
    bool IsRenewal,
    long AskDollars,
    long CurrentSalary);

/// <summary>A place the team has to fill: a seat or a key role nobody holds for the time ahead.</summary>
/// <param name="Subject">The subject (<c>driver</c> or <c>staff:Role</c>).</param>
/// <param name="Count">How many people are missing.</param>
/// <param name="DaysEmpty">How long it has stood empty, as far as the world shows it.</param>
public sealed record Vacancy(string Subject, int Count, int DaysEmpty);

/// <summary>Everything the market decider reads. All of it is what the team's own books and its manager's views show.</summary>
public sealed record MarketInput(
    DateOnly Today,
    AiFunds Funds,
    IReadOnlyList<Incumbent> Incumbents,
    IReadOnlyList<Candidate> Candidates,
    IReadOnlyList<Talk> Talks,
    IReadOnlyList<Vacancy> Vacancies,
    IReadOnlyList<string> DriverSeatsHeld,
    int FreeTalkSlots);

/// <summary>What a market review asks the host to do.</summary>
public abstract record MarketAction
{
    private MarketAction()
    {
    }

    /// <summary>Renew a contract on the given terms (the renewal command opens the talk and makes the offer in one step).</summary>
    public sealed record Renew(string ContractId, string PersonId, string Subject, AiOffer Offer) : MarketAction;

    /// <summary>The team lets a contract run out. Nothing is filed; the decision is traced.</summary>
    public sealed record LetExpire(string ContractId, string PersonId) : MarketAction;

    /// <summary>Open a talk with a person. The terms follow on the next review (the host cannot name the new talk before it exists).</summary>
    public sealed record Approach(string PersonId, string Subject) : MarketAction;

    /// <summary>Make or revise an offer in an open talk.</summary>
    public sealed record SubmitOffer(string NegotiationId, AiOffer Offer) : MarketAction;

    /// <summary>Accept the terms the person holds to.</summary>
    public sealed record Accept(string NegotiationId) : MarketAction;

    /// <summary>End a talk.</summary>
    public sealed record WalkAway(string NegotiationId) : MarketAction;
}
