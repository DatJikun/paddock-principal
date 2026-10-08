using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Development;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Development;

/// <summary>
/// One lived day of car development for every team that has cars, the player's and the AI's alike. It names no manager:
/// a plan is data in the development section, whoever set it. Writes nothing when nothing changed.
/// <para>
/// On a morning the host has marked with <see cref="CareerEventType.SeasonChanged"/> (order 5, before this handler) it devalues
/// each account against the new regulations. It does not itself move the cars: that already happened in the host's season change.
/// When an inbox and the managers are given, the engineers tell the managers who run a team what happened (PP-066): a part arrived (the
/// area, the range before and after, and where that puts the team against the top three), a new concept is ready (a decision with
/// numbers), a concept failed, a concept went live. An AI principal gets none of it: it reads the same numbers from the query.
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
        TellThePrincipals(before, outcome, inputs);
    }

    /// <summary>Everything the engineers report to a human principal after this day's step. Reads the section before and after.</summary>
    private void TellThePrincipals(DevelopmentSection before, DevelopmentOutcome outcome, DevelopmentInputs inputs)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        var after = outcome.Development;
        foreach (var project in after.Projects)
        {
            if (before.Find(project.Id) is not { } was || was.Status == project.Status)
            {
                continue;
            }

            switch (project.Kind)
            {
                case DevKind.Upgrade when was.Status == ProjectStatus.Active && project.Status is ProjectStatus.Completed or ProjectStatus.Failed:
                    ReportPart(project, outcome, inputs);
                    break;
                case DevKind.Concept when project.IsRedesign && was.Status == ProjectStatus.Active && project.Status == ProjectStatus.Ready:
                    AskAboutNewConcept(project, outcome, inputs);
                    break;
                case DevKind.Concept when project.IsRedesign && was.Status == ProjectStatus.Active && project.Status == ProjectStatus.Failed:
                    Tell(project, DevelopmentKeys.ConceptFailedKind, DevelopmentKeys.ConceptFailedSubject, [], inputs);
                    break;
                case DevKind.Concept when project.IsRedesign && was.Status == ProjectStatus.InProduction && project.Status == ProjectStatus.Deployed:
                    ReportLive(project, outcome, inputs);
                    break;
                case DevKind.Concept when !project.IsRedesign && was.Status == ProjectStatus.Active && project.Status == ProjectStatus.Ready:
                    AskAboutOldConcept(project, inputs);
                    break;
            }
        }
    }

    /// <summary>A part arrived (or did not work): the area, the range of it before and after, and the top three of the grid in that area.</summary>
    private void ReportPart(DevProject project, DevelopmentOutcome outcome, DevelopmentInputs inputs)
    {
        if (project.Area is not { } devArea)
        {
            return;
        }

        var area = DisplayArea(devArea);
        var readBefore = DevelopmentQuery.ReadOut(inputs.World, inputs.Cars, inputs.Finance, inputs.Development, _environment.Races, project.Organization, inputs.Today);
        var readAfter = DevelopmentQuery.ReadOut(_book.World, outcome.Cars, outcome.Finance.HasBooks ? outcome.Finance : inputs.Finance, outcome.Development, _environment.Races, project.Organization, inputs.Today);
        var was = readBefore.Areas.FirstOrDefault(item => item.Area == area);
        var now = readAfter.Areas.FirstOrDefault(item => item.Area == area);
        if (was is null || now is null)
        {
            return;
        }

        var ours = (now.Own.Low + now.Own.High) / 2d;
        var behind = now.Rivals.Count(rival => ((rival.Band.Low + rival.Band.High) / 2d) < ours);
        var variant = project.Status == ProjectStatus.Failed ? "failed" : project.IsBreakthrough ? "big" : "ok";
        Tell(
            project,
            DevelopmentKeys.PartKind,
            DevelopmentKeys.PartSubject(variant, area),
            [
                new KeyValuePair<string, string>("before", Range(was.Own)),
                new KeyValuePair<string, string>("after", Range(now.Own)),
                new KeyValuePair<string, string>("rivals", string.Join("; ", now.Rivals.Select(rival => rival.Name + " " + Range(rival.Band)))),
                new KeyValuePair<string, string>("behind", behind.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("total", now.Rivals.Count.ToString(CultureInfo.InvariantCulture)),
            ],
            inputs);
    }

    /// <summary>A new concept is ready: the decision with numbers. Commit now, or keep developing (the default after the deadline).</summary>
    private void AskAboutNewConcept(DevProject project, DevelopmentOutcome outcome, DevelopmentInputs inputs)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        var read = DevelopmentQuery.ReadOut(_book.World, outcome.Cars, outcome.Finance.HasBooks ? outcome.Finance : inputs.Finance, outcome.Development, _environment.Races, project.Organization, inputs.Today);
        if (read.Next.Decision is not { } decision)
        {
            return;
        }

        var arguments = new List<KeyValuePair<string, string>>
        {
            new(DevelopmentKeys.ProjectArgument, project.Id),
            new("concept", decision.Name),
            new("ceiling", Range(decision.Ceiling)),
            new("ceilingNow", Range(decision.CeilingNow)),
            new("gainLow", Signed(decision.Gain.Low)),
            new("gainHigh", Signed(decision.Gain.High)),
            new("start", Range(decision.StartLevel)),
            new("levelNow", Range(decision.LevelNow)),
            new("days", decision.BuildDays.ToString(CultureInfo.InvariantCulture)),
            new("amount", new Paddock.Domain.Finance.Money(decision.CostCents).WholeDollars.ToString(CultureInfo.InvariantCulture)),
        };
        if (decision.FirstRace is { } race)
        {
            arguments.Add(new KeyValuePair<string, string>("date", race.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        var subject = DevelopmentKeys.NewConceptSubject(decision.Breakthrough, decision.FirstRace is not null);
        foreach (var manager in HumanManagersOf(project))
        {
            var draft = new InboxItemDraft(
                DevelopmentKeys.ConceptInboxKind,
                subject,
                arguments,
                [
                    new InboxOption(DevelopmentKeys.OptionCommit, DevelopmentKeys.NewConceptCommitLabel, DevelopmentKeys.NewConceptCommitConsequence),
                    new InboxOption(DevelopmentKeys.OptionWait, DevelopmentKeys.NewConceptWaitLabel, DevelopmentKeys.NewConceptWaitConsequence),
                ],
                inputs.Today.AddDays(DevelopmentEstimates.ConceptDecisionDays),
                DevelopmentKeys.OptionWait);
            _inbox.Post(_managers, manager, draft, inputs.Today);
        }
    }

    /// <summary>The new concept is in the car: what it starts at, and that the team does not understand it yet.</summary>
    private void ReportLive(DevProject project, DevelopmentOutcome outcome, DevelopmentInputs inputs)
    {
        var read = DevelopmentQuery.ReadOut(_book.World, outcome.Cars, outcome.Finance.HasBooks ? outcome.Finance : inputs.Finance, outcome.Development, _environment.Races, project.Organization, inputs.Today);
        var total = read.Areas.FirstOrDefault(item => item.Area == DevelopmentQuery.AreaTotal);
        Tell(
            project,
            DevelopmentKeys.ConceptLiveKind,
            DevelopmentKeys.ConceptLiveSubject,
            [
                new KeyValuePair<string, string>("concept", read.Concept.Name),
                new KeyValuePair<string, string>("level", total is null ? "-" : Range(total.Own)),
                new KeyValuePair<string, string>("understanding", Range(read.Understanding.Level)),
            ],
            inputs);
    }

    /// <summary>
    /// A concept of the first model that was running yesterday and is ready today, and that no timing will commit by itself, asks its
    /// human principal. A <c>WhenReady</c> or <c>AfterRaces</c> concept is committed by the day step, so it needs no question.
    /// </summary>
    private void AskAboutOldConcept(DevProject project, DevelopmentInputs inputs)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        var plan = ConceptProduction.Plan(inputs.World, inputs.Finance, project.Organization, project, inputs.Today);
        foreach (var manager in HumanManagersOf(project))
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

    private void Tell(DevProject project, string kind, string subject, IReadOnlyList<KeyValuePair<string, string>> arguments, DevelopmentInputs inputs)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        foreach (var manager in HumanManagersOf(project))
        {
            var draft = new InboxItemDraft(kind, subject, arguments.Append(new KeyValuePair<string, string>(DevelopmentKeys.ProjectArgument, project.Id)), null, null, null);
            _inbox.Post(_managers, manager, draft, inputs.Today);
        }
    }

    /// <summary>The managers who run the team and are human. An AI principal answers nothing, so a question to it would hold the shared clock for ever.</summary>
    private IEnumerable<ManagerId> HumanManagersOf(DevProject project) =>
        _environment.Control.ManagersOf(project.Organization)
            .Where(manager => _managers is not null && _managers.Contains(manager) && _managers.KindOf(manager) == ManagerKind.Human);

    /// <summary>The area a part feeds, in the four the principal reads: chassis and tyres together are handling.</summary>
    internal static string DisplayArea(DevArea area) => area switch
    {
        DevArea.Aero => DevelopmentQuery.AreaAero,
        DevArea.Reliability => DevelopmentQuery.AreaReliability,
        _ => DevelopmentQuery.AreaHandling,
    };

    private static string Range(Paddock.Application.Cars.CarBandView band) =>
        ((int)Math.Round(band.Low, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
        + "–"
        + ((int)Math.Round(band.High, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static string Signed(double value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded > 0 ? "+" + rounded.ToString(CultureInfo.InvariantCulture) : rounded.ToString(CultureInfo.InvariantCulture);
    }
}
