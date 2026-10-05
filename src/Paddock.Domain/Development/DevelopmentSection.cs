using System.Collections.Immutable;
using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Development;

/// <summary>
/// Plans, accounts and projects of car development, section <see cref="SectionName"/>. Immutable. Project numbers only move
/// forward (INV-009). Truth stays here; a manager reads <c>DevelopmentQuery</c>. A save with no development has no section.
/// <para>
/// Canonical text (schema 1), after the section header. Money is integer cents, shares and stock are milli-units.
/// <code>
/// next-project &lt;n&gt;
/// plans &lt;count&gt;
/// plan &lt;len&gt;:&lt;org&gt; &lt;current&gt; &lt;account&gt; &lt;next&gt; &lt;aero&gt; &lt;chassis&gt; &lt;reliability&gt; &lt;tyres&gt; &lt;spentCur&gt; &lt;spentAcc&gt; &lt;spentNext&gt; &lt;changed or -&gt;
/// accounts &lt;count&gt;
/// account &lt;len&gt;:&lt;org&gt; &lt;stock&gt; &lt;nextYear&gt; &lt;rulesYear&gt;
/// projects &lt;count&gt;
/// project &lt;number&gt; &lt;len&gt;:&lt;org&gt; &lt;kind&gt; &lt;area or -&gt; &lt;len&gt;:&lt;engineer&gt; &lt;started&gt; &lt;duration&gt; &lt;progress&gt; &lt;cost&gt; &lt;posted&gt;
///   &lt;share&gt; &lt;risk&gt; &lt;outcome or -&gt; &lt;status&gt; &lt;timing&gt; &lt;timingRaces&gt; &lt;waited&gt; &lt;closed or -&gt; [prod &lt;ends&gt; &lt;productionCost&gt;]
/// </code>
/// The trailing <c>prod</c> group is written only for a project that was committed to production (T42c), so a section
/// without such a project keeps the text, and the hash, it had before.
/// <code>
/// </code>
/// </para>
/// </summary>
public sealed class DevelopmentSection : IWorldSection
{
    public const string SectionName = "development";

    private readonly SortedDictionary<string, DevelopmentPlan> _plans;
    private readonly SortedDictionary<string, DevelopmentAccount> _accounts;
    private readonly ImmutableSortedDictionary<long, DevProject> _projects;

    /// <summary>
    /// Projects the day still walks (active, in production, ready), per organization, in project-number order. Closed history
    /// stays in <see cref="_projects"/> for the save and the queries; the daily step must not copy it.
    /// </summary>
    private readonly ImmutableDictionary<string, ImmutableArray<DevProject>> _open;

    private DevelopmentSection(
        long nextProject,
        SortedDictionary<string, DevelopmentPlan> plans,
        SortedDictionary<string, DevelopmentAccount> accounts,
        ImmutableSortedDictionary<long, DevProject> projects,
        ImmutableDictionary<string, ImmutableArray<DevProject>> open)
    {
        NextProject = nextProject;
        _plans = plans;
        _accounts = accounts;
        _projects = projects;
        _open = open;
    }

    public static DevelopmentSection Empty { get; } = new(
        1,
        new SortedDictionary<string, DevelopmentPlan>(StringComparer.Ordinal),
        new SortedDictionary<string, DevelopmentAccount>(StringComparer.Ordinal),
        ImmutableSortedDictionary<long, DevProject>.Empty,
        ImmutableDictionary<string, ImmutableArray<DevProject>>.Empty);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long NextProject { get; }

    public bool IsEmpty => NextProject == 1 && _plans.Count == 0 && _accounts.Count == 0 && _projects.Count == 0;

    public IReadOnlyList<DevelopmentPlan> Plans => _plans.Values.ToArray();

    public IReadOnlyList<DevelopmentAccount> Accounts => _accounts.Values.ToArray();

    public IReadOnlyList<DevProject> Projects => _projects.Values.ToArray();

    public DevelopmentPlan? PlanOf(OrganizationId organization) =>
        organization.IsAssigned && _plans.TryGetValue(organization.Value, out var plan) ? plan : null;

    public DevelopmentAccount AccountOf(OrganizationId organization) =>
        organization.IsAssigned && _accounts.TryGetValue(organization.Value, out var account)
            ? account
            : DevelopmentAccount.Empty(organization);

    public DevProject? Find(string id) =>
        DevProjectIds.TryParse(id, out var number) && _projects.TryGetValue(number, out var project) ? project : null;

    public IReadOnlyList<DevProject> ProjectsOf(OrganizationId organization) =>
        _projects.Values.Where(project => project.Organization == organization).ToArray();

    /// <summary>The organization's projects the day still has to advance. Empty when it has none.</summary>
    public IReadOnlyList<DevProject> OpenOf(OrganizationId organization) =>
        organization.IsAssigned && _open.TryGetValue(organization.Value, out var projects)
            ? projects
            : [];

    public static DevelopmentSection Restore(
        long nextProject,
        IEnumerable<DevelopmentPlan> plans,
        IEnumerable<DevelopmentAccount> accounts,
        IEnumerable<DevProject> projects)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextProject, 1);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(projects);
        var planMap = new SortedDictionary<string, DevelopmentPlan>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            Validate(plan);
            if (!planMap.TryAdd(plan.Organization.Value, plan))
            {
                throw new InvalidOperationException($"Organization '{plan.Organization}' has two plans.");
            }
        }

        var accountMap = new SortedDictionary<string, DevelopmentAccount>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            ArgumentNullException.ThrowIfNull(account);
            if (!account.Organization.IsAssigned || account.StockMilli is < 0 or > 100_000 || account.NextYearShareMilli is < 0 or > 1000)
            {
                throw new InvalidOperationException("A development account is out of range.");
            }

            if (!accountMap.TryAdd(account.Organization.Value, account))
            {
                throw new InvalidOperationException($"Organization '{account.Organization}' has two accounts.");
            }
        }

        var projectMap = ImmutableSortedDictionary.CreateBuilder<long, DevProject>();
        foreach (var project in projects)
        {
            ArgumentNullException.ThrowIfNull(project);
            if (project.Number >= nextProject)
            {
                throw new InvalidOperationException($"Project '{project.Id}' is not below the counter {nextProject}.");
            }

            if ((project.Status == ProjectStatus.InProduction && project.ProductionEnds is null) || project.ProductionCostCents < 0)
            {
                throw new InvalidOperationException($"Project '{project.Id}' is in production without a finish date.");
            }

            if (projectMap.ContainsKey(project.Number))
            {
                throw new InvalidOperationException($"Project '{project.Id}' appears twice.");
            }

            projectMap[project.Number] = project;
        }

        var stored = projectMap.ToImmutable();
        return new DevelopmentSection(nextProject, planMap, accountMap, stored, IndexOpen(stored));
    }

    public DevelopmentSection SetPlan(DevelopmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Validate(plan);
        var plans = new SortedDictionary<string, DevelopmentPlan>(_plans, StringComparer.Ordinal) { [plan.Organization.Value] = plan };
        return new DevelopmentSection(NextProject, plans, _accounts, _projects, _open);
    }

    public DevelopmentSection SetAccount(DevelopmentAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var accounts = new SortedDictionary<string, DevelopmentAccount>(_accounts, StringComparer.Ordinal)
        {
            [account.Organization.Value] = account,
        };
        return new DevelopmentSection(NextProject, _plans, accounts, _projects, _open);
    }

    /// <summary>Adds a project. <paramref name="project"/> must carry the number <see cref="NextProject"/>.</summary>
    public DevelopmentSection AddProject(DevProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Number != NextProject)
        {
            throw new InvalidOperationException($"The next project number is {NextProject}.");
        }

        var projects = _projects.SetItem(project.Number, project);
        return new DevelopmentSection(NextProject + 1, _plans, _accounts, projects, TouchOpen(project));
    }

    public DevelopmentSection ReplaceProject(DevProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_projects.ContainsKey(project.Number))
        {
            throw new InvalidOperationException($"Project '{project.Id}' is not in the section.");
        }

        var projects = _projects.SetItem(project.Number, project);
        return new DevelopmentSection(NextProject, _plans, _accounts, projects, TouchOpen(project));
    }

    private ImmutableDictionary<string, ImmutableArray<DevProject>> TouchOpen(DevProject project)
    {
        var org = project.Organization.Value;
        var listed = _open.TryGetValue(org, out var current);
        var index = -1;
        if (listed)
        {
            for (var i = 0; i < current.Length; i++)
            {
                if (current[i].Number == project.Number)
                {
                    index = i;
                    break;
                }
            }
        }

        if (!project.IsOpen)
        {
            if (index < 0)
            {
                return _open;
            }

            return current.Length == 1 ? _open.Remove(org) : _open.SetItem(org, current.RemoveAt(index));
        }

        if (!listed)
        {
            return _open.SetItem(org, ImmutableArray.Create(project));
        }

        // Numbers only rise, so a new project appends in the same order as the full scan.
        return index < 0
            ? _open.SetItem(org, current.Add(project))
            : _open.SetItem(org, current.SetItem(index, project));
    }

    private static ImmutableDictionary<string, ImmutableArray<DevProject>> IndexOpen(ImmutableSortedDictionary<long, DevProject> projects)
    {
        var builders = new Dictionary<string, ImmutableArray<DevProject>.Builder>(StringComparer.Ordinal);
        foreach (var project in projects.Values)
        {
            if (!project.IsOpen)
            {
                continue;
            }

            if (!builders.TryGetValue(project.Organization.Value, out var builder))
            {
                builder = ImmutableArray.CreateBuilder<DevProject>();
                builders.Add(project.Organization.Value, builder);
            }

            builder.Add(project);
        }

        var open = ImmutableDictionary.CreateBuilder<string, ImmutableArray<DevProject>>(StringComparer.Ordinal);
        foreach (var (org, builder) in builders)
        {
            open[org] = builder.DrainToImmutable();
        }

        return open.ToImmutable();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next-project", NextProject);
        writer.Count("plans", _plans.Count);
        foreach (var plan in _plans.Values)
        {
            writer.Begin("plan");
            writer.Field(plan.Organization.Value);
            writer.Raw(
                " " + N(plan.CurrentPercent) + " " + N(plan.AccountPercent) + " " + N(plan.NextYearPercent)
                + " " + N(plan.AeroPriority) + " " + N(plan.ChassisPriority) + " " + N(plan.ReliabilityPriority) + " " + N(plan.TyresPriority)
                + " " + N(plan.SpentCurrentCents) + " " + N(plan.SpentAccountCents) + " " + N(plan.SpentNextYearCents)
                + " " + D(plan.ChangedOn));
            writer.End();
        }

        writer.Count("accounts", _accounts.Count);
        foreach (var account in _accounts.Values)
        {
            writer.Begin("account");
            writer.Field(account.Organization.Value);
            writer.Raw(" " + N(account.StockMilli) + " " + N(account.NextYearShareMilli) + " " + N(account.RulesYear));
            writer.End();
        }

        writer.Count("projects", _projects.Count);
        foreach (var project in _projects.Values)
        {
            writer.Begin("project");
            writer.Raw(N(project.Number) + " ");
            writer.Field(project.Organization.Value);
            writer.Raw(" " + project.Kind + " " + (project.Area is { } area ? area.ToString() : "-") + " ");
            writer.Field(project.Engineer);
            writer.Raw(
                " " + D(project.Started) + " " + N(project.DurationDays) + " " + N(project.ProgressDays)
                + " " + N(project.CostCents) + " " + N(project.PostedCents)
                + " " + N(project.ShareMilli) + " " + N(project.RiskMilli)
                + " " + (project.OutcomeMilli is { } outcome ? N(outcome) : "-")
                + " " + project.Status + " " + project.Timing + " " + N(project.TimingRaces) + " " + N(project.RacesWaited)
                + " " + D(project.ClosedOn)
                + (project.ProductionEnds is { } ends ? " prod " + D(ends) + " " + N(project.ProductionCostCents) : string.Empty));
            writer.End();
        }
    }

    private static void Validate(DevelopmentPlan plan)
    {
        if (!plan.Organization.IsAssigned
            || !DevelopmentPlan.IsValidSplit(plan.CurrentPercent, plan.AccountPercent, plan.NextYearPercent)
            || !DevelopmentPlan.IsValidPriority(plan.AeroPriority)
            || !DevelopmentPlan.IsValidPriority(plan.ChassisPriority)
            || !DevelopmentPlan.IsValidPriority(plan.ReliabilityPriority)
            || !DevelopmentPlan.IsValidPriority(plan.TyresPriority)
            || plan.SpentCurrentCents < 0
            || plan.SpentAccountCents < 0
            || plan.SpentNextYearCents < 0)
        {
            throw new InvalidOperationException($"The development plan of '{plan.Organization}' is out of range.");
        }
    }

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string D(GameDate? date) => date is { } value ? value.ToString() : "-";
}
