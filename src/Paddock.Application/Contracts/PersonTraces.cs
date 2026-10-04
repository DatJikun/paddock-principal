using System.Globalization;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// Builds the <see cref="DecisionTrace"/> of a person's decision (TECH section 7): the person acts as an actor, so every answer to
/// an offer, every choice among rival offers, and every use of an exit clause or an option leaves one. Building a trace reads
/// values the decision already computed; it never draws RNG and never changes state (INV-005, INV-006). Only what the person
/// states is marked player-visible: the terms named in the reasons, and the option chosen. The weights and the personality are
/// truth context, for the developer only.
/// </summary>
public static class PersonTraces
{
    public const string TriggerResponse = "negotiation.response";

    public const string TriggerChoice = "negotiation.choose";

    public const string TriggerExit = "contract.exit";

    public const string TriggerOption = "contract.option";

    public static DecisionTrace ForResponse(
        Negotiation negotiation,
        PersonResponse response,
        EvaluationContext context,
        GameDate today)
    {
        var stated = StatedTerms(response.Reasons);
        var chosen = response.Kind switch
        {
            ResponseKind.Accept => "accept",
            ResponseKind.Counter => "counter",
            _ => "refuse",
        };
        var options = new List<TraceOption>
        {
            new("accept", response.Evaluation.Utility, Factors(response.Evaluation, stated), chosen == "accept"),
        };
        if (response.Counter is not null)
        {
            var counter = CounterpartyEvaluator.Evaluate(response.Counter, context);
            options.Add(new TraceOption("counter", counter.Utility, Factors(counter, stated), chosen == "counter"));
        }

        options.Add(new TraceOption("refuse", response.Floor, [], chosen == "refuse"));
        var truth = TruthOf(context.Traits);
        truth["negotiation"] = negotiation.Id;
        truth["floor"] = Number(response.Floor);
        truth["threshold"] = Number(response.Threshold);
        truth["interest"] = negotiation.Interest.ToString(CultureInfo.InvariantCulture);
        truth["referenceSalary"] = context.ReferenceSalary.ToString(CultureInfo.InvariantCulture);
        return new DecisionTrace(
            new WeekendKey(today.Season, 0),
            negotiation.Counterparty.Value,
            NegotiationEstimates.PersonDeciderLevel,
            TriggerResponse + ":" + negotiation.Id,
            options,
            chosen,
            "utility " + Number(response.Evaluation.Utility) + " against a threshold of " + Number(response.Threshold),
            response.Reasons.Count == 0 ? null : string.Join(",", response.Reasons),
            chosen == "accept",
            truth);
    }

    public static DecisionTrace ForChoice(
        PersonId person,
        IReadOnlyList<AcceptableOffer> candidates,
        AcceptableOffer winner,
        GameDate today)
    {
        var options = candidates
            .Select(candidate => new TraceOption(
                candidate.Negotiation.Id,
                candidate.Utility,
                [new TraceFactor("utility", candidate.Utility, false)],
                candidate.Negotiation.Id == winner.Negotiation.Id))
            .ToArray();
        return new DecisionTrace(
            new WeekendKey(today.Season, 0),
            person.Value,
            NegotiationEstimates.PersonDeciderLevel,
            TriggerChoice,
            options,
            winner.Negotiation.Id,
            "best of " + candidates.Count.ToString(CultureInfo.InvariantCulture) + " acceptable offers",
            null,
            true,
            new Dictionary<string, string> { ["offers"] = string.Join(",", candidates.Select(candidate => candidate.Negotiation.Id)) });
    }

    public static DecisionTrace ForStayOrLeave(
        string trigger,
        Contract contract,
        double stay,
        double leave,
        bool takeAction,
        string actionOption,
        string keepOption,
        EvaluationContext context,
        GameDate today)
    {
        var chosen = takeAction ? actionOption : keepOption;
        var truth = TruthOf(context.Traits);
        truth["contract"] = contract.Id.Value;
        return new DecisionTrace(
            new WeekendKey(today.Season, 0),
            contract.PersonId.Value,
            NegotiationEstimates.PersonDeciderLevel,
            trigger + ":" + contract.Id.Value,
            [
                new TraceOption(keepOption, stay, [], chosen == keepOption),
                new TraceOption(actionOption, leave, [], chosen == actionOption),
            ],
            chosen,
            "staying is worth " + Number(stay) + " against " + Number(leave),
            null,
            takeAction,
            truth);
    }

    private static IReadOnlyList<TraceFactor> Factors(OfferEvaluation evaluation, ISet<string> stated) =>
        evaluation.Factors
            .Select(factor => new TraceFactor(factor.Name, factor.Contribution, stated.Contains(factor.Name)))
            .ToArray();

    /// <summary>The terms of the utility the person names in the reasons: the only factors a manager may see.</summary>
    private static HashSet<string> StatedTerms(IReadOnlyList<string> reasons)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reason in reasons)
        {
            switch (reason)
            {
                case NegotiationReasons.SalaryTooLow:
                    terms.Add(UtilityTerms.Salary);
                    break;
                case NegotiationReasons.Status:
                    terms.Add(UtilityTerms.Status);
                    break;
                case NegotiationReasons.TeamTooWeak:
                    terms.Add(UtilityTerms.Prestige);
                    terms.Add(UtilityTerms.Car);
                    break;
                case NegotiationReasons.Risk:
                    terms.Add(UtilityTerms.Risk);
                    break;
            }
        }

        return terms;
    }

    private static Dictionary<string, string> TruthOf(PersonalityTraits traits) => new(StringComparer.Ordinal)
    {
        ["personality.primary"] = traits.Primary.ToString(),
        ["personality.loyalty"] = traits.Loyalty.ToString(CultureInfo.InvariantCulture),
        ["personality.ambition"] = traits.Ambition.ToString(CultureInfo.InvariantCulture),
        ["personality.temperament"] = traits.Temperament.ToString(CultureInfo.InvariantCulture),
        ["personality.professionalism"] = traits.Professionalism.ToString(CultureInfo.InvariantCulture),
        ["personality.ego"] = traits.Ego.ToString(CultureInfo.InvariantCulture),
    };

    private static string Number(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
