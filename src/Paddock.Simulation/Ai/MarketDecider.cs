using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>
/// The driver and key-staff market of an AI principal (DESIGN section 8, T39): renewals, filling vacancies from the people with no
/// contract, and answering counters. It reads only <see cref="MarketInput"/>, which is made of beliefs and the team's own books, and
/// it picks every option by maximum utility with a factor breakdown (<see cref="UtilityChooser"/>). A weak principal misjudges more
/// (noise by tier), looks at fewer candidates and plans less; an archetype changes what it values (<see cref="ArchetypeProfile"/>).
/// </summary>
public static class MarketDecider
{
    public const string RenewalKind = "market.renewal";

    public const string SearchKind = "market.search";

    public const string OfferKind = "market.offer";

    public const string ReplyKind = "market.reply";

    public const string AgreedKind = "market.agreed";

    public static IReadOnlyList<MarketAction> Review(DecisionContext context, MarketInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var actions = new List<MarketAction>();
        var inTalk = new HashSet<string>(StringComparer.Ordinal);
        foreach (var talk in input.Talks)
        {
            inTalk.Add(talk.Person.Id);
        }

        foreach (var incumbent in input.Incumbents)
        {
            // A person the team keeps cannot also be a candidate for the seat he already holds.
            inTalk.Add(incumbent.Person.Id);
        }

        var slots = input.FreeTalkSlots;

        foreach (var talk in input.Talks.OrderBy(talk => talk.NegotiationId, StringComparer.Ordinal))
        {
            switch (talk.State)
            {
                case TalkState.NeedsOffer:
                    NeedsOffer(context, input, talk, actions);
                    break;
                case TalkState.Countered:
                    Countered(context, input, talk, actions);
                    break;
                case TalkState.PersonAgreed:
                    Agreed(context, input, talk, actions);
                    break;
            }
        }

        foreach (var incumbent in input.Incumbents.OrderBy(item => item.ContractId, StringComparer.Ordinal))
        {
            if (incumbent.RenewalOpen
                || incumbent.RenewedAlready
                || incumbent.End < input.Today
                || input.Today.DayNumber + AiEstimates.RenewalLeadDays < incumbent.End.DayNumber
                || slots <= 0)
            {
                continue;
            }

            if (Renewal(context, input, incumbent, inTalk, actions))
            {
                slots--;
            }
        }

        foreach (var vacancy in input.Vacancies.OrderBy(item => item.Subject, StringComparer.Ordinal))
        {
            for (var index = 0; index < vacancy.Count && slots > 0; index++)
            {
                var chosen = Search(context, input, vacancy, index, inTalk, actions);
                if (chosen is null)
                {
                    break;
                }

                inTalk.Add(chosen);
                slots--;
            }
        }

        return actions;
    }

    // ---------------------------------------------------------------- utility terms

    /// <summary>The terms of the utility of having <paramref name="person"/> for <paramref name="subject"/> at <paramref name="priceDollars"/> a year.</summary>
    internal static List<FactorDraft> Terms(DecisionContext context, AiFunds funds, PersonView person, string subject, long priceDollars, bool incumbent)
    {
        var profile = context.Profile;
        var driver = MarketSubjects.IsDriver(subject);
        var perceived = person.Quality.Perceived(profile.RiskAversion);
        var weight = driver ? profile.Now : (profile.Now + profile.Future) / 2.0 * AiEstimates.StaffQualityWeight;
        var factors = new List<FactorDraft>
        {
            new(AiTextKeys.FactorQuality, weight * AiEstimates.QualityScale * (perceived - AiEstimates.ReferenceStars)),
        };

        if (driver && person.Age <= AiEstimates.YoungTalentAge && person.Potential.IsKnown)
        {
            var potential = person.Potential.Perceived(profile.RiskAversion);
            factors.Add(new FactorDraft(AiTextKeys.FactorPotential, profile.Upside * AiEstimates.QualityScale * Math.Max(0.0, potential - perceived)));
        }

        if (driver)
        {
            var old = Math.Max(0, person.Age - AiEstimates.PrimeAgeTo);
            var young = Math.Max(0, AiEstimates.PrimeAgeFrom - person.Age);
            var age = -(profile.Now * AiEstimates.AgePenaltyPerYearOld * old) - (profile.RiskAversion * AiEstimates.AgePenaltyPerYearYoung * young);
            factors.Add(new FactorDraft(AiTextKeys.FactorAge, age));
        }

        var basis = Math.Max(1L, funds.PayrollLimit(profile));
        factors.Add(new FactorDraft(AiTextKeys.FactorPrice, -profile.Thrift * AiEstimates.PriceScale * priceDollars / basis));

        if (incumbent)
        {
            var people = context.Skills.PeopleManagement / (double)AiEstimates.LevelCount;
            factors.Add(new FactorDraft(AiTextKeys.FactorContinuity, profile.Stability * AiEstimates.ContinuityBonus * (0.5 + (0.5 * people))));
        }

        factors.Add(new FactorDraft(AiTextKeys.FactorStyle, Style(context, person, priceDollars, basis, perceived), PlayerVisible: false));
        return factors;
    }

    private static double Style(DecisionContext context, PersonView person, long price, long basis, double perceived) => context.Archetype switch
    {
        PrincipalArchetype.Contender => perceived >= 3.5 ? 0.10 : 0.0,
        PrincipalArchetype.Builder => person.Age <= AiEstimates.PrimeAgeFrom + 2 ? 0.10 : 0.0,
        PrincipalArchetype.Opportunist => person.Age >= 30 && perceived >= AiEstimates.ReferenceStars ? 0.10 : 0.0,
        PrincipalArchetype.Survivor => price * 4 < basis ? 0.10 : 0.0,
        _ => 0.0,
    };

    private static double Sum(IReadOnlyList<FactorDraft> factors)
    {
        var total = 0.0;
        foreach (var factor in factors)
        {
            total += factor.Contribution;
        }

        return total;
    }

    private static int Shortlist(DecisionContext context) =>
        AiEstimates.ShortlistByLevel[context.Skills.For(DecisionFacet.Market) - 1];

    // ---------------------------------------------------------------- terms of an offer

    /// <summary>The share of the ask an offer opens at: a good negotiator opens lower, a generous archetype higher.</summary>
    internal static long OpeningSalary(DecisionContext context, long ask, long current, bool urgent)
    {
        var tier = context.Skills.Negotiation;
        var share = AiEstimates.OpeningShareByNegotiation[tier - 1] + (0.5 * context.Profile.PayStretch) + (urgent ? 0.05 : 0.0);
        var basis = Math.Max(ask, current > 0 ? (long)Math.Round(current * 0.9, MidpointRounding.AwayFromZero) : 0L);
        return Math.Max(1L, (long)Math.Round(basis * Math.Max(0.5, share), MidpointRounding.AwayFromZero));
    }

    /// <summary>The most the principal pays for a person: the ask (or the current pay of a renewal) with the archetype's stretch.</summary>
    internal static long Ceiling(DecisionContext context, long ask, long current)
    {
        var basis = Math.Max(ask, current);
        return Math.Max(1L, (long)Math.Round(basis * (AiEstimates.BaseCeilingShare + context.Profile.PayStretch), MidpointRounding.AwayFromZero));
    }

    public static int Years(DecisionContext context, PersonView person, string subject)
    {
        int years;
        var planner = context.Profile.Stability >= 0.8 && context.Level >= AiEstimates.PlanningLevel;
        if (!MarketSubjects.IsDriver(subject))
        {
            years = planner ? AiEstimates.LongContractYears : AiEstimates.OrdinaryContractYears;
        }
        else if (planner && person.Age <= AiEstimates.YoungTalentAge)
        {
            years = AiEstimates.LongContractYears;
        }
        else if (context.Profile.RiskAversion >= 0.8 || person.Age > AiEstimates.PrimeAgeTo)
        {
            years = 1;
        }
        else
        {
            years = AiEstimates.OrdinaryContractYears;
        }

        if (context.Level < AiEstimates.PlanningLevel)
        {
            years = Math.Min(years, AiEstimates.ShortContractYears);
        }

        return years;
    }

    internal static string? Seat(DecisionContext context, PersonView person, string subject, IReadOnlyList<string> held)
    {
        if (!MarketSubjects.IsDriver(subject))
        {
            return null;
        }

        if (held.Contains(AiSeats.NumberOne))
        {
            return AiSeats.NumberTwo;
        }

        if (held.Contains(AiSeats.NumberTwo))
        {
            return AiSeats.NumberOne;
        }

        if (held.Count > 0)
        {
            return AiSeats.Equal;
        }

        return context.Archetype == PrincipalArchetype.Contender && person.Quality.Perceived(context.Profile.RiskAversion) >= 3.0
            ? AiSeats.NumberOne
            : AiSeats.Equal;
    }

    // ---------------------------------------------------------------- the three kinds of talk

    private static void NeedsOffer(DecisionContext context, MarketInput input, Talk talk, List<MarketAction> actions)
    {
        var urgent = Vacancy(input, talk.Subject)?.DaysEmpty >= AiEstimates.MaxVacancyDays;
        var salary = OpeningSalary(context, talk.AskDollars, talk.CurrentSalary, urgent);
        var note = new TraceNote(
            OfferKind,
            talk.NegotiationId,
            talk.NegotiationId,
            "Names its terms for " + talk.Person.Id + " in " + talk.Subject + ".",
            AiTextKeys.ReasonOffer,
            IsKeyDecision: false);
        if (!input.Funds.CanCommit(salary, input.Today))
        {
            UtilityChooser.Record(
                context,
                note with { PlayerReasonKey = AiTextKeys.ReasonWalked, Reason = "Cannot afford its own opening offer; the talk ends." },
                [Option(AiTextKeys.OptionWalk, AiTextKeys.FactorAffordability, -1.0)],
                AiTextKeys.OptionWalk);
            actions.Add(new MarketAction.WalkAway(talk.NegotiationId));
            return;
        }

        var offer = new AiOffer(salary, Years(context, talk.Person, talk.Subject), Seat(context, talk.Person, talk.Subject, input.DriverSeatsHeld));
        var basis = Math.Max(1L, input.Funds.PayrollLimit(context.Profile));
        UtilityChooser.Record(
            context,
            note,
            [Option(AiTextKeys.OptionOffer, AiTextKeys.FactorPrice, -context.Profile.Thrift * AiEstimates.PriceScale * salary / basis)],
            AiTextKeys.OptionOffer);
        actions.Add(new MarketAction.SubmitOffer(talk.NegotiationId, offer));
    }

    private static void Agreed(DecisionContext context, MarketInput input, Talk talk, List<MarketAction> actions)
    {
        var affordable = input.Funds.CanCommit(talk.CounterSalary, input.Today);
        var note = new TraceNote(
            AgreedKind,
            talk.NegotiationId,
            talk.NegotiationId,
            affordable ? "The person agrees to the offer; the team signs." : "The person agrees but the team cannot carry the pay; it walks away.",
            affordable ? AiTextKeys.ReasonAccepted : AiTextKeys.ReasonWalked,
            IsKeyDecision: MarketSubjects.IsDriver(talk.Subject));
        UtilityChooser.Record(
            context,
            note,
            [
                Option(AiTextKeys.OptionAccept, AiTextKeys.FactorAffordability, affordable ? 1.0 : -1.0),
                Option(AiTextKeys.OptionWalk, AiTextKeys.FactorAffordability, 0.0),
            ],
            affordable ? AiTextKeys.OptionAccept : AiTextKeys.OptionWalk);
        actions.Add(affordable ? new MarketAction.Accept(talk.NegotiationId) : new MarketAction.WalkAway(talk.NegotiationId));
    }

    private static void Countered(DecisionContext context, MarketInput input, Talk talk, List<MarketAction> actions)
    {
        var funds = input.Funds;
        var ceiling = Ceiling(context, talk.AskDollars, talk.CurrentSalary);
        var basis = Math.Max(1L, Math.Max(talk.AskDollars, talk.CurrentSalary));
        var options = new List<OptionDraft>();

        if (funds.CanCommit(talk.CounterSalary, input.Today))
        {
            var accept = Terms(context, funds, talk.Person, talk.Subject, talk.CounterSalary, talk.IsRenewal);
            if (talk.CounterSalary > ceiling)
            {
                accept.Add(new FactorDraft(AiTextKeys.FactorOverpay, -(double)(talk.CounterSalary - ceiling) / basis));
            }

            options.Add(new OptionDraft(AiTextKeys.OptionAccept, accept));
        }

        if (talk.RoundsLeft > 0 && talk.CounterSalary <= ceiling * AiEstimates.ReviseWithinShare && talk.OfferSalary > 0)
        {
            var revised = talk.OfferSalary + (long)Math.Round((talk.CounterSalary - talk.OfferSalary) * AiEstimates.ReviseGapShare, MidpointRounding.AwayFromZero);
            revised = Math.Max(revised, talk.OfferSalary + 1);
            if (funds.CanCommit(revised, input.Today) && revised < talk.CounterSalary)
            {
                var terms = Terms(context, funds, talk.Person, talk.Subject, revised, talk.IsRenewal);
                if (revised > ceiling)
                {
                    terms.Add(new FactorDraft(AiTextKeys.FactorOverpay, -(double)(revised - ceiling) / basis));
                }

                terms.Add(new FactorDraft(AiTextKeys.FactorRisk, -0.05));
                options.Add(new OptionDraft(AiTextKeys.OptionRevise, terms));
            }
        }

        var alternative = BestAlternative(context, input, talk.Subject, new HashSet<string>(StringComparer.Ordinal) { talk.Person.Id });
        var walkFactors = new List<FactorDraft>();
        if (alternative is { } best)
        {
            walkFactors.Add(new FactorDraft(AiTextKeys.FactorBestAlternative, best));
            walkFactors.Add(new FactorDraft(AiTextKeys.FactorSearchCost, -AiEstimates.SearchFriction));
        }
        else
        {
            walkFactors.Add(new FactorDraft(AiTextKeys.FactorWaitCost, AiEstimates.NoAlternativeUtility));
        }

        options.Add(new OptionDraft(AiTextKeys.OptionWalk, walkFactors));

        var note = new TraceNote(
            ReplyKind,
            talk.NegotiationId,
            talk.NegotiationId,
            "Weighs the person's counter of " + talk.CounterSalary.ToString(CultureInfo.InvariantCulture) + " against its ceiling of " + ceiling.ToString(CultureInfo.InvariantCulture) + ".",
            null,
            IsKeyDecision: MarketSubjects.IsDriver(talk.Subject));
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Market, note, options, Reasons);
        switch (chosen.Id)
        {
            case AiTextKeys.OptionAccept:
                actions.Add(new MarketAction.Accept(talk.NegotiationId));
                break;
            case AiTextKeys.OptionRevise:
                var salary = Math.Max(talk.OfferSalary + 1, talk.OfferSalary + (long)Math.Round((talk.CounterSalary - talk.OfferSalary) * AiEstimates.ReviseGapShare, MidpointRounding.AwayFromZero));
                actions.Add(new MarketAction.SubmitOffer(
                    talk.NegotiationId,
                    new AiOffer(salary, talk.CounterYears > 0 ? talk.CounterYears : talk.OfferYears, talk.CounterSeat)));
                break;
            default:
                actions.Add(new MarketAction.WalkAway(talk.NegotiationId));
                break;
        }
    }

    // ---------------------------------------------------------------- renewals

    private static bool Renewal(DecisionContext context, MarketInput input, Incumbent incumbent, HashSet<string> inTalk, List<MarketAction> actions)
    {
        var funds = input.Funds;
        var salary = OpeningSalary(context, incumbent.AskDollars, incumbent.SalaryDollars, urgent: false);
        var options = new List<OptionDraft>();
        var affordable = funds.CanCommit(salary, input.Today);
        if (affordable)
        {
            options.Add(new OptionDraft(AiTextKeys.OptionRenew, Terms(context, funds, incumbent.Person, incumbent.Subject, salary, incumbent: true)));
        }

        var alternative = BestAlternative(context, input, incumbent.Subject, inTalk);
        var letFactors = new List<FactorDraft>();
        if (alternative is { } best)
        {
            letFactors.Add(new FactorDraft(AiTextKeys.FactorBestAlternative, best));
            letFactors.Add(new FactorDraft(AiTextKeys.FactorSearchCost, -AiEstimates.SearchFriction));
        }
        else
        {
            letFactors.Add(new FactorDraft(AiTextKeys.FactorWaitCost, AiEstimates.NoAlternativeUtility));
        }

        options.Add(new OptionDraft(AiTextKeys.OptionLetExpire, letFactors));

        var driver = MarketSubjects.IsDriver(incumbent.Subject);
        var note = new TraceNote(
            RenewalKind,
            incumbent.ContractId,
            incumbent.ContractId,
            "Weighs keeping " + incumbent.Person.Id + " (contract ends " + incumbent.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ") against the market.",
            null,
            IsKeyDecision: driver || incumbent.Subject == "staff:TechnicalDirector");
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Market, note, options, Reasons);
        if (chosen.Id == AiTextKeys.OptionRenew)
        {
            var offer = new AiOffer(salary, Years(context, incumbent.Person, incumbent.Subject), incumbent.Seat);
            actions.Add(new MarketAction.Renew(incumbent.ContractId, incumbent.Person.Id, incumbent.Subject, offer));
            return true;
        }

        actions.Add(new MarketAction.LetExpire(incumbent.ContractId, incumbent.Person.Id));
        return false;
    }

    // ---------------------------------------------------------------- vacancies

    private static Vacancy? Vacancy(MarketInput input, string subject)
    {
        foreach (var vacancy in input.Vacancies)
        {
            if (vacancy.Subject == subject)
            {
                return vacancy;
            }
        }

        return null;
    }

    private static List<(Candidate Candidate, List<FactorDraft> Factors, double Score, long Opening)> Ranked(
        DecisionContext context,
        MarketInput input,
        string subject,
        HashSet<string> excluded,
        bool urgent)
    {
        var ranked = new List<(Candidate Candidate, List<FactorDraft> Factors, double Score, long Opening)>();
        foreach (var candidate in input.Candidates)
        {
            if (candidate.Subject != subject || excluded.Contains(candidate.Person.Id))
            {
                continue;
            }

            var opening = OpeningSalary(context, candidate.AskDollars, 0, urgent);
            if (!input.Funds.CanCommit(opening, input.Today))
            {
                continue;
            }

            var factors = Terms(context, input.Funds, candidate.Person, subject, opening, incumbent: false);
            ranked.Add((candidate, factors, Sum(factors), opening));
        }

        ranked.Sort(static (left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(left.Candidate.Person.Id, right.Candidate.Person.Id);
        });
        return ranked;
    }

    /// <summary>The best utility the market offers for a subject without noise, or null when nobody is available and affordable.</summary>
    private static double? BestAlternative(DecisionContext context, MarketInput input, string subject, HashSet<string> excluded)
    {
        var ranked = Ranked(context, input, subject, excluded, urgent: false);
        return ranked.Count == 0 ? null : ranked[0].Score;
    }

    private static string? Search(DecisionContext context, MarketInput input, Vacancy vacancy, int index, HashSet<string> inTalk, List<MarketAction> actions)
    {
        var urgent = vacancy.DaysEmpty >= AiEstimates.MaxVacancyDays;
        var ranked = Ranked(context, input, vacancy.Subject, inTalk, urgent);
        var take = Math.Min(Shortlist(context), ranked.Count);
        var options = new List<OptionDraft>(take + 1);
        for (var i = 0; i < take; i++)
        {
            options.Add(new OptionDraft(AiTextKeys.OptionCandidate + "/" + ranked[i].Candidate.Person.Id, ranked[i].Factors));
        }

        if (!urgent)
        {
            var waited = AiEstimates.WaitUtility + (AiEstimates.WaitUtilityPerWeek * (vacancy.DaysEmpty / 7));
            options.Add(new OptionDraft(AiTextKeys.OptionWait, [new FactorDraft(AiTextKeys.FactorWaitCost, waited)]));
        }

        if (options.Count == 0)
        {
            return null;
        }

        var note = new TraceNote(
            SearchKind,
            vacancy.Subject + "/" + index.ToString(CultureInfo.InvariantCulture),
            vacancy.Subject,
            "Looks for " + vacancy.Subject + " among " + take.ToString(CultureInfo.InvariantCulture) + " shortlisted people.",
            null,
            IsKeyDecision: MarketSubjects.IsDriver(vacancy.Subject) || vacancy.Subject == "staff:TechnicalDirector");
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Market, note, options, Reasons);
        if (chosen.Id == AiTextKeys.OptionWait)
        {
            return null;
        }

        var person = ranked[chosen.Index].Candidate.Person.Id;
        actions.Add(new MarketAction.Approach(person, vacancy.Subject));
        return person;
    }

    private static OptionDraft Option(string id, string factor, double value) => new(id, [new FactorDraft(factor, value)]);

    /// <summary>The reason a player may read for the option that won. Only what a player could know of the choice is in it.</summary>
    private static string? Reasons(string optionId)
    {
        if (optionId.StartsWith(AiTextKeys.OptionCandidate, StringComparison.Ordinal))
        {
            return AiTextKeys.ReasonSigned;
        }

        return optionId switch
        {
            AiTextKeys.OptionRenew => AiTextKeys.ReasonRenewed,
            AiTextKeys.OptionLetExpire => AiTextKeys.ReasonLetExpire,
            AiTextKeys.OptionWait => AiTextKeys.ReasonWaited,
            AiTextKeys.OptionAccept => AiTextKeys.ReasonAccepted,
            AiTextKeys.OptionRevise => AiTextKeys.ReasonRevised,
            AiTextKeys.OptionWalk => AiTextKeys.ReasonWalked,
            _ => null,
        };
    }
}
