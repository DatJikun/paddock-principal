using Paddock.Application.Board;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Newspaper;
using Paddock.Application.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Desktop.Bridge;

public sealed partial class CareerBridge
{
    /// <summary>The paper of the main screen (#324): a pure read of what the career stored, in the player's view.</summary>
    private NewspaperView ReadNewspaper()
    {
        var inputs = Box.Inputs;
        var plans = new Dictionary<int, IReadOnlyList<SeasonCalendar.PlannedSession>>();
        RaceDay? DayOf(int season, int round)
        {
            if (!plans.TryGetValue(season, out var plan))
            {
                plan = SeasonPlans.Stored(Session.World, season)
                    ?? (inputs.Layouts is { } layouts && inputs.RaceAssignments is { } assignments
                        ? SeasonPlans.Read(Session.World, season, layouts, assignments)
                        : []);
                plans[season] = plan;
            }

            foreach (var session in plan)
            {
                if (session.TypeId == ScheduledEventType.Race && session.Round == round)
                {
                    return new RaceDay(session.Date, session.LayoutId);
                }
            }

            return null;
        }

        var organization = Box.Require<BoardBook>().Section.OrganizationOf(Human.Value);
        return NewspaperRead.Read(new NewspaperSources
        {
            World = Session.World,
            Today = Session.Date,
            Own = organization ?? default,
            Contracts = Box.TryGet<ContractBook>()?.Section,
            Inbox = new InboxQuery(Inbox()).View(Access()).Items,
            RaceDays = DayOf,
            CircuitName = layoutId => Circuits.TryGetValue(layoutId, out var circuit) ? circuit.Name : layoutId,
        });
    }
}
