using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Development;
using Paddock.Domain.Inbox;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Development;

/// <summary>
/// One lived day of car development for every team that has cars, the player's and the AI's alike. It names no manager:
/// a plan is data in the development section, whoever set it. Writes nothing when nothing changed.
/// <para>
/// On a morning the host has marked with <see cref="CareerEventType.SeasonChanged"/> (order 5, before this handler) it devalues
/// each account against the new regulations. It does not itself move the cars: that already happened in the host's season change.
/// When an inbox and the managers are given, a concept that becomes ready and is not set to commit by itself puts the decision
/// "commit now or keep developing?" in the inbox of the managers who run the team (T42c).
/// </para>
/// </summary>
public sealed class DevelopmentDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after sponsors (750) so the day's income is booked, before the finance review (800) so it sees this spending.</summary>
    public const int DefaultOrder = 780;

    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;
    private readonly InboxBook? _inbox;
    private readonly ManagerRegistry? _managers;

    public DevelopmentDayHandler(
        DevelopmentBook book,
        DevelopmentEnvironment environment,
        int order = DefaultOrder,
        InboxBook? inbox = null,
        ManagerRegistry? managers = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        Order = order;
        _inbox = inbox;
        _managers = managers;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_book.Cars.Cars.Count == 0)
        {
            return;
        }

        var before = _book.Section;
        var inputs = _book.Inputs(context.Today, _environment);
        if (context.HasEmitted(CareerEventType.SeasonChanged))
        {
            var season = DevelopmentEngine.ApplyNewRegulations(inputs);
            if (season.Changed)
            {
                _book.Write(season);
                inputs = _book.Inputs(context.Today, _environment);
            }
        }

        var outcome = DevelopmentEngine.Step(inputs);
        if (!outcome.Changed)
        {
            return;
        }

        _book.Write(outcome);
        AskAboutReadyConcepts(before, outcome.Development, inputs);
    }

    /// <summary>
    /// A concept that was running yesterday and is ready today, and that no timing will commit by itself, asks its principal.
    /// A <c>WhenReady</c> or <c>AfterRaces</c> concept is committed by the day step, so it needs no question.
    /// </summary>
    private void AskAboutReadyConcepts(DevelopmentSection before, DevelopmentSection after, DevelopmentInputs inputs)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        foreach (var project in after.Projects.Where(project => project.Kind == DevKind.Concept && project.Status == ProjectStatus.Ready))
        {
            if (before.Find(project.Id) is not { Status: ProjectStatus.Active })
            {
                continue;
            }

            var plan = ConceptProduction.Plan(inputs.World, inputs.Finance, project.Organization, project, inputs.Today);
            foreach (var manager in _environment.Control.ManagersOf(project.Organization))
            {
                var draft = new InboxItemDraft(
                    DevelopmentKeys.ConceptInboxKind,
                    DevelopmentKeys.ConceptSubject,
                    [
                        new KeyValuePair<string, string>(DevelopmentKeys.ProjectArgument, project.Id),
                        new KeyValuePair<string, string>("days", plan.Days.ToString(CultureInfo.InvariantCulture)),
                    ],
                    [
                        new InboxOption(DevelopmentKeys.OptionCommit, DevelopmentKeys.ConceptCommitLabel, DevelopmentKeys.ConceptCommitConsequence),
                        new InboxOption(DevelopmentKeys.OptionWait, DevelopmentKeys.ConceptWaitLabel, DevelopmentKeys.ConceptWaitConsequence),
                    ],
                    inputs.Today.AddDays(DevelopmentEstimates.ConceptDecisionDays),
                    DevelopmentKeys.OptionWait);
                _inbox.Post(_managers, manager, draft, inputs.Today);
            }
        }
    }
}
