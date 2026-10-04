using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Career;

/// <summary>
/// Placeholder until T44 (issue #109): each morning the AI employer looks at driver contracts inside the renewal window.
/// ESTIMATE: it renews when a draw from <see cref="RngStreamName.AiDecisions"/> is below <see cref="RenewBelow"/>, offering the
/// current terms for <see cref="ContractEngine.DefaultRenewalYears"/> seasons. A counter or an agreement is accepted.
/// The decision is a <see cref="DecisionTrace"/> on the contract environment's sink. It does not read hidden attributes.
/// </summary>
public static class AiContractPlaceholder
{
    /// <summary>ESTIMATE: renew when the AiDecisions draw is strictly below this. 1 would renew every contract in the window.</summary>
    public const double RenewBelow = 0.8;

    public const string Trigger = "contract.renewal.placeholder";

    public static void File(CareerSession session, ContractEngine engine, CommandQueue queue, ManagerId manager, ITraceSink trace)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(trace);
        var today = session.Date;
        var issued = new DateOnly(today.Year, today.Month, today.Day);
        var stream = session.BorrowStream(RngStreamName.AiDecisions);
        var drew = false;

        foreach (var negotiation in engine.Book.Section.Active())
        {
            if (negotiation.ManagerId != manager.Value)
            {
                continue;
            }

            if (negotiation.Status is not (NegotiationStatus.Countered or NegotiationStatus.PersonAgreed))
            {
                continue;
            }

            queue.Enqueue(new AcceptCounterOfferCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                NegotiationId = negotiation.Id,
            });
            Record(trace, manager, "accept-counter", negotiation.Id, today);
        }

        foreach (var contract in engine.Book.World.Contracts.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            if (!contract.Role.IsDriver || !contract.IsActiveOn(today))
            {
                continue;
            }

            if (today.DaysUntil(contract.End) > NegotiationEstimates.RenewalPromptDays)
            {
                continue;
            }

            if (engine.Book.Section.Active().Any(negotiation => negotiation.RenewalOf == contract.Id))
            {
                continue;
            }

            drew = true;
            var roll = stream.NextDouble();
            if (roll >= RenewBelow)
            {
                Record(trace, manager, "let-expire", contract.Id.Value, today);
                continue;
            }

            queue.Enqueue(new RenewContractCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                Contract = contract.Id,
                Offer = engine.DefaultRenewalOffer(contract, today),
            });
            Record(trace, manager, "renew", contract.Id.Value, today);
        }

        if (drew)
        {
            session.KeepStream(RngStreamName.AiDecisions, stream);
        }
    }

    private static void Record(ITraceSink trace, ManagerId manager, string chosen, string subject, GameDate today)
    {
        if (!trace.IsEnabled)
        {
            return;
        }

        trace.Record(new DecisionTrace(
            new WeekendKey(today.Year, 0),
            manager.Value,
            0,
            Trigger,
            [
                new TraceOption("renew", 1, [], PlayerVisible: false),
                new TraceOption("let-expire", 0, [], PlayerVisible: false),
                new TraceOption("accept-counter", 1, [], PlayerVisible: false),
            ],
            chosen,
            "ESTIMATE placeholder until T44: AiDecisions draw below " + RenewBelow.ToString(System.Globalization.CultureInfo.InvariantCulture) + " renews.",
            null,
            IsKeyDecision: false,
            new Dictionary<string, string> { ["subject"] = subject }));
    }
}
