using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>One team's day of development. A small mutable bag over the immutable sections, thrown away after the step.</summary>
internal sealed class TeamDay
{
    private readonly OrganizationId _organization;
    private readonly DevelopmentInputs _inputs;
    private readonly DevelopmentEra _era;
    private readonly DevelopmentPlan _planBefore;
    private readonly DevelopmentAccount _accountBefore;
    private DevelopmentPlan _plan;
    private DevelopmentAccount _account;

    public TeamDay(
        OrganizationId organization,
        CarsSection cars,
        DevelopmentSection development,
        FinanceSection finance,
        DevelopmentInputs inputs,
        DevelopmentEra era)
    {
        _organization = organization;
        Cars = cars;
        Development = development;
        Finance = finance;
        _inputs = inputs;
        _era = era;
        _plan = development.PlanOf(organization) ?? DevelopmentPlan.Default(organization);
        _planBefore = _plan;
        _account = development.AccountOf(organization);
        _accountBefore = _account;
    }

    public CarsSection Cars { get; private set; }

    public DevelopmentSection Development { get; private set; }

    public FinanceSection Finance { get; private set; }

    public bool Changed { get; private set; }

    private Paddock.Domain.Time.GameDate Today => _inputs.Today;

    public void Run()
    {
        AdvanceProjects();
        AdvanceProduction();
        DeployReady();
        StartProjects();
        GrowUnderstanding();
        _account = _account with { StockMilli = DevelopmentMath.StockAfterDay(_account.StockMilli) };
        Commit();
    }

    /// <summary>
    /// Reacts to <c>season.changed</c>: the new year's regulations devalue the account. The host has already moved the cars
    /// (<see cref="ChangeSeason"/>). Once <see cref="DevelopmentAccount.RulesYear"/> is this year, a second call changes nothing.
    /// </summary>
    public void ApplyNewRegulations()
    {
        DevalueOnRuleChange();
        Commit();
    }

    /// <summary>
    /// The change of season for this team: next season's concepts are consumed, the cars move to the new year with the work carried
    /// over, and the year's spending resets. The host calls it once on 1 January (owner decision, #160); the daily step never does.
    /// </summary>
    public void ChangeSeason()
    {
        Rollover();
        Commit();
    }

    /// <summary>Closes an active project early for a proportional share and a proportional cost.</summary>
    public void Cut(DevProject project)
    {
        var fraction = project.Progress;
        var target = (long)Math.Round(project.CostCents * fraction, MidpointRounding.AwayFromZero);
        PostSpend(project, target - project.PostedCents);
        _plan = _plan.WithSpent(project.Kind, target - project.CostCents);
        var noise = DevelopmentMath.Noise(OutcomeRng(project).NextDouble());
        var share = Math.Min(1d, project.ShareMilli / 1000d * noise * fraction);
        var closed = project with { CostCents = target, PostedCents = target };
        Finish(closed, share, failed: false, cut: true);
        Commit();
    }

    /// <summary>Deploys a finished concept now, whatever its timing, with no production. Kept for old callers; see <see cref="StartProductionNow"/>.</summary>
    public void DeployNow(DevProject project)
    {
        Deploy(project);
        Commit();
    }

    /// <summary>Commits a finished concept to production now (the principal's decision, or <c>WhenReady</c>).</summary>
    public void StartProductionNow(DevProject project)
    {
        StartProduction(project);
        Commit();
    }

    private void DevalueOnRuleChange()
    {
        if (_account.RulesYear == Today.Year)
        {
            return;
        }

        var stock = _account.StockMilli;
        if (_account.RulesYear != 0)
        {
            var (changed, total) = _era.ChangedSince(_inputs.Rules.Era(_account.RulesYear));
            stock = DevelopmentMath.StockAfterRuleChange(stock, changed, total);
        }

        _account = _account with { StockMilli = stock, RulesYear = Today.Year };
    }

    private void Rollover()
    {
        // Only concepts timed for the next season are consumed here; a held (or still waiting) concept stays Ready.
        foreach (var project in Projects().Where(project => project.Status == ProjectStatus.Ready && project.Timing == ConceptTiming.NextSeason))
        {
            var share = (project.OutcomeMilli ?? 0) / 1000d;
            _account = _account with { NextYearShareMilli = DevelopmentMath.NextYearShareAfter(_account.NextYearShareMilli, share) };
            Replace(project with { Status = ProjectStatus.Deployed, ClosedOn = Today });
        }

        var carried = _account.NextYearShareMilli / 1000d;
        foreach (var car in Cars.Of(_organization))
        {
            if (car.Season >= Today.Year)
            {
                continue;
            }

            var levels = car.Levels;
            foreach (var area in Enum.GetValues<DevArea>())
            {
                var gain = DevelopmentMath.Gain(DevelopmentMath.Headroom(car, area), carried);
                levels = DevelopmentMath.WithLevel(levels, area, DevelopmentMath.LevelOf(levels, area) + gain);
            }

            var understanding = CarEstimates.Quantize(
                car.Understanding + ((CarEstimates.NewConceptUnderstanding - car.Understanding) * carried));
            Cars = Cars.Replace(car.WithDesign(
                Today.Year,
                car.Concept,
                levels,
                car.ConceptCeiling,
                Math.Clamp(understanding, 0d, 100d),
                car.TyreWearMultiplier,
                car.SupplierChangeCost));
            Changed = true;
        }

        _account = _account with { NextYearShareMilli = 0 };
        _plan = _plan.ResetSpent();
    }

    private void AdvanceProjects()
    {
        foreach (var project in Projects().Where(project => project.IsActive).ToArray())
        {
            var next = project with { ProgressDays = project.ProgressDays + 1 };
            var done = next.ProgressDays >= next.DurationDays;
            var target = (long)Math.Round(next.CostCents * next.Progress, MidpointRounding.AwayFromZero);
            var due = target - next.PostedCents;
            if (done || next.ProgressDays % DevelopmentEstimates.PostEveryDays == 0)
            {
                next = PostSpend(next, due);
            }

            if (!done)
            {
                Replace(next);
                continue;
            }

            var rng = OutcomeRng(next);
            var noise = DevelopmentMath.Noise(rng.NextDouble());
            var failed = rng.NextDouble() < next.RiskMilli / 1000d;
            var share = failed ? 0d : Math.Min(1d, next.ShareMilli / 1000d * noise);
            Finish(next, share, failed, cut: false);
        }
    }

    /// <summary>
    /// A concept in production goes live on the first day after its last production day, so no race on the finish day or before
    /// sees it: the car never changes in the middle of a race weekend, whatever the order of the day's steps.
    /// </summary>
    private void AdvanceProduction()
    {
        foreach (var project in Projects().Where(project => project.IsInProduction))
        {
            if (project.ProductionEnds is { } ends && Today > ends)
            {
                Deploy(project);
            }
        }
    }

    /// <summary>The automatic path: <c>WhenReady</c> commits as soon as the concept is ready, <c>AfterRaces</c> once the races are counted.</summary>
    private void DeployReady()
    {
        foreach (var project in Projects().Where(project => project.Status == ProjectStatus.Ready))
        {
            var due = project.Timing == ConceptTiming.WhenReady
                || (project.Timing == ConceptTiming.AfterRaces && project.RacesWaited >= project.TimingRaces);
            if (due)
            {
                StartProduction(project);
            }
        }
    }

    /// <summary>Starts production: the old car keeps racing, the cost goes to the ledger now, the concept goes live when production ends.</summary>
    private void StartProduction(DevProject project)
    {
        var plan = ConceptProduction.Plan(_inputs.World, Finance, _organization, project, Today);
        if (plan.CostCents > 0 && Finance.HasBook(_organization))
        {
            Finance = Finance.Post(_organization, Today, LedgerCategories.Development, project.Id, -plan.CostCents, DevelopmentKeys.LedgerProduction);
        }

        Replace(project with
        {
            Status = ProjectStatus.InProduction,
            ProductionEnds = Today.AddDays(plan.Days),
            ProductionCostCents = plan.CostCents,
        });
    }

    private void Deploy(DevProject project)
    {
        var share = (project.OutcomeMilli ?? 0) / 1000d;
        foreach (var car in Cars.Of(_organization))
        {
            var levels = car.Levels;
            foreach (var area in Enum.GetValues<DevArea>())
            {
                var gain = DevelopmentMath.Gain(DevelopmentMath.Headroom(car, area), share);
                levels = DevelopmentMath.WithLevel(levels, area, DevelopmentMath.LevelOf(levels, area) + gain);
            }

            Cars = Cars.Replace(DevelopmentEngine.Restamp(car, levels, Math.Min(car.Understanding, CarEstimates.NewConceptUnderstanding)));
        }

        Replace(project with { Status = ProjectStatus.Deployed, ClosedOn = Today });
    }

    /// <summary>Applies a finished (or cut) project with its realised share.</summary>
    private void Finish(DevProject project, double share, bool failed, bool cut)
    {
        var outcome = DevelopmentEstimates.Milli(share);
        switch (project.Kind)
        {
            case DevKind.Upgrade:
                if (share > 0d && _account.StockMilli > 0)
                {
                    var (bonus, left) = DevelopmentMath.DrawOnAccount(_account.StockMilli);
                    share = Math.Min(1d, share * (1d + bonus));
                    _account = _account with { StockMilli = left };
                    outcome = DevelopmentEstimates.Milli(share);
                }

                var area = project.Area ?? throw new InvalidOperationException("An upgrade has an area.");
                foreach (var car in Cars.Of(_organization))
                {
                    var gain = DevelopmentMath.Gain(DevelopmentMath.Headroom(car, area), share);
                    var levels = DevelopmentMath.WithLevel(car.Levels, area, DevelopmentMath.LevelOf(car.Levels, area) + gain);
                    var understanding = Math.Max(0d, car.Understanding - (DevelopmentEstimates.UpgradeUnderstandingHit * share));
                    Cars = Cars.Replace(DevelopmentEngine.Restamp(car, levels, CarEstimates.Quantize(understanding)));
                }

                Replace(project with
                {
                    OutcomeMilli = outcome,
                    Status = failed ? ProjectStatus.Failed : cut ? ProjectStatus.Cut : ProjectStatus.Completed,
                    ClosedOn = Today,
                });
                break;
            case DevKind.Research:
                _account = _account with { StockMilli = DevelopmentMath.StockAfterResearch(_account.StockMilli, share) };
                Replace(project with
                {
                    OutcomeMilli = outcome,
                    Status = failed ? ProjectStatus.Failed : cut ? ProjectStatus.Cut : ProjectStatus.Completed,
                    ClosedOn = Today,
                });
                break;
            default:
                var status = failed ? ProjectStatus.Failed : share > 0d ? ProjectStatus.Ready : ProjectStatus.Cut;
                Replace(project with
                {
                    OutcomeMilli = outcome,
                    Status = status,
                    ClosedOn = status == ProjectStatus.Ready ? null : Today,
                });
                break;
        }

        Changed = true;
    }

    private void StartProjects()
    {
        var annual = DevelopmentMath.AnnualBudgetCents(Finance.TypicalCents);
        var cars = Cars.Of(_organization);
        if (annual <= 0d || cars.Count == 0)
        {
            return;
        }

        var running = Projects().Where(project => project.IsActive).ToList();
        if (running.Count >= DevelopmentEstimates.MaxSlots)
        {
            return;
        }

        var engineers = EngineerRoster.Of(_inputs.World, _organization, Today);
        var balance = Finance.HasBook(_organization) ? Finance.BalanceOf(_organization) : 0L;
        var capacity = EngineeringCapacity.Derive(engineers, Today.Year, EngineerRoster.ChairsIn(Today.Year), balance, (long)annual);
        for (var slot = running.Count; slot < capacity.Slots; slot++)
        {
            var decision = EngineerChoice.Choose(
                _organization,
                engineers,
                _plan,
                cars,
                _account,
                running.Select(project => (project.Kind, project.Area)).ToArray(),
                Today,
                _inputs.MasterSeed,
                slot);
            if (decision is null)
            {
                return;
            }

            var chosen = decision.Chosen;
            var cost = DevelopmentMath.CostCents(annual, _plan.PercentOf(chosen.Kind), chosen.Kind, Today.Year);
            if (cost <= 0)
            {
                return;
            }

            var share = DevelopmentMath.ExpectedShare(chosen.Kind, cost, annual, capacity.Quality, Today.Year);
            var risk = DevelopmentMath.Risk(chosen.Kind, chosen.Skill, Today.Year);
            var project = new DevProject(
                Development.NextProject,
                _organization,
                chosen.Kind,
                chosen.Area,
                chosen.Engineer.Id,
                Today,
                capacity.DurationDays(DevelopmentMath.BaseDays(chosen.Kind, Today.Year)),
                0,
                cost,
                0,
                DevelopmentEstimates.Milli(share),
                DevelopmentEstimates.Milli(risk),
                null,
                ProjectStatus.Active,
                ConceptTiming.NextSeason,
                0,
                0,
                null);
            Development = Development.AddProject(project);
            _plan = _plan.WithSpent(chosen.Kind, cost);
            running.Add(project);
            Changed = true;
            if (_inputs.Trace.IsEnabled)
            {
                _inputs.Trace.Record(EngineerChoice.Trace(_organization, decision, Today, "development.choose"));
            }
        }
    }

    private void GrowUnderstanding()
    {
        var active = Projects().Count(project => project.IsActive);
        var points = DevelopmentEstimates.UnderstandingPerDay + (DevelopmentEstimates.UnderstandingPerActiveProject * active);
        var cap = _era.UnderstandingCap;
        foreach (var car in Cars.Of(_organization))
        {
            var grown = DevelopmentMath.GrowUnderstanding(car.Understanding, points, cap);
            if (grown != car.Understanding)
            {
                Cars = Cars.Replace(DevelopmentEngine.Restamp(car, car.Levels, grown));
                Changed = true;
            }
        }
    }

    private DevProject PostSpend(DevProject project, long due)
    {
        if (due <= 0)
        {
            return project;
        }

        if (Finance.HasBook(_organization))
        {
            Finance = Finance.Post(_organization, Today, LedgerCategories.Development, project.Id, -due, DevelopmentKeys.LedgerSpend);
            Changed = true;
        }

        return project with { PostedCents = project.PostedCents + due };
    }

    private Xoshiro256StarStar OutcomeRng(DevProject project) =>
        RngStream
            .Derive(_inputs.MasterSeed, RngStreamName.Development, Today.Year)
            .DeriveChild(DevelopmentEngine.OutcomeKey(project));

    private IReadOnlyList<DevProject> Projects() => Development.OpenOf(_organization);

    private void Replace(DevProject project)
    {
        Development = Development.ReplaceProject(project);
        Changed = true;
    }

    private void Commit()
    {
        if (_plan != _planBefore)
        {
            Development = Development.SetPlan(_plan);
            Changed = true;
        }

        if (_account != _accountBefore)
        {
            Development = Development.SetAccount(_account);
            Changed = true;
        }
    }
}
