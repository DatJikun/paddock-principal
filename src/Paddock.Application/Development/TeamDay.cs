using Paddock.Application.Infrastructure;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>
/// What one finished project drew from its <c>Development</c> child: the execution noise, whether it failed, and whether the
/// engineer had a breakthrough. Drawn in this order, so adding a draw later never shifts an earlier one (INV-004).
/// </summary>
internal readonly record struct ProjectDraw(Xoshiro256StarStar Rng, double Noise, bool Failed, bool Breakthrough, int Innovation);

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
        EnsureConceptYear();
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
        var closed = project with { CostCents = target, PostedCents = target };
        var draw = Draw(closed);
        if (closed.IsRedesign)
        {
            FinishRedesign(closed, draw, fraction, cut: true);
        }
        else
        {
            var noise = DevelopmentMath.Noise(OutcomeRng(project).NextDouble());
            var share = Math.Min(1d, project.ShareMilli / 1000d * noise * fraction);
            Finish(closed, share, failed: false, cut: true, breakthrough: false);
        }

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

    /// <summary>The year the concept in the car was introduced, set the first day the team is seen with a car (PP-066: it names the concept).</summary>
    private void EnsureConceptYear()
    {
        if (_account.ConceptYear != 0)
        {
            return;
        }

        var cars = Cars.Of(_organization);
        if (cars.Count > 0)
        {
            _account = _account with { ConceptYear = cars[0].Season };
        }
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
        // A concept timed for the next season goes into production on the first morning of the year (v2) or is consumed into the
        // carried share of next year's car (a concept of the first model). A held one, or one still waiting, stays ready.
        foreach (var project in Projects().Where(project => project.Status == ProjectStatus.Ready && project.Timing == ConceptTiming.NextSeason))
        {
            if (project.IsRedesign)
            {
                StartProduction(project);
                continue;
            }

            var share = (project.OutcomeMilli ?? 0) / 1000d;
            _account = _account with { NextYearShareMilli = DevelopmentMath.NextYearShareAfter(_account.NextYearShareMilli, share) };
            Replace(project with { Status = ProjectStatus.Deployed, ClosedOn = Today });
        }

        var carried = _account.NextYearShareMilli / 1000d;
        var aging = DevelopmentEstimates.ConceptAgingPerSeason * DevelopmentMath.GainScale(Today.Year);
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
                Math.Max(0d, car.ConceptCeiling - aging),
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

            var draw = Draw(next);
            if (next.IsRedesign)
            {
                FinishRedesign(next, draw, 1d, cut: false);
                continue;
            }

            var share = draw.Failed ? 0d : Math.Min(1d, next.ShareMilli / 1000d * draw.Noise);
            Finish(next, share, draw.Failed, cut: false, draw.Breakthrough);
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
        if (project.IsRedesign)
        {
            DeployRedesign(project);
            return;
        }

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

    /// <summary>
    /// A new concept replaces the one in the car (PP-066): the drawn ceiling, the chosen aero and philosophy, a start at the share of
    /// that ceiling the philosophy gives, and understanding back to the level of a new car. The old concept's work is gone.
    /// </summary>
    private void DeployRedesign(DevProject project)
    {
        var ceiling = project.CeilingMilli / 1000d;
        double? before = null;
        double after = 0d;
        foreach (var car in Cars.Of(_organization))
        {
            var concept = DevelopmentMath.Redesigned(car.Concept, project.PhilosophyMilli, project.AeroMilli);
            var effects = ConceptMapping.Effects(concept, ceiling);
            var levels = DevelopmentMath.StartLevels(concept, ceiling, project.PhilosophyMilli);
            var understanding = Math.Min(car.Understanding, CarEstimates.NewConceptUnderstanding);
            before ??= car.Understanding;
            after = understanding;
            Cars = Cars.Replace(car.WithDesign(
                car.Season,
                concept,
                levels,
                ceiling,
                understanding,
                effects.TyreWearMultiplier,
                effects.SupplierChangeCost));
        }

        Note(UnderstandingSources.Concept, (before ?? after) - after);
        _account = _account with { ConceptYear = Today.Year };
        Replace(project with { Status = ProjectStatus.Deployed, ClosedOn = Today });
    }

    /// <summary>The three draws every finished project takes, in a fixed order. The ceiling of a concept follows from the same child.</summary>
    private ProjectDraw Draw(DevProject project)
    {
        var rng = OutcomeRng(project);
        var innovation = InnovationOf(project);
        var noise = DevelopmentMath.Noise(rng.NextDouble(), innovation);
        var failed = rng.NextDouble() < project.RiskMilli / 1000d;
        var breakthrough = rng.NextDouble() < DevelopmentMath.BreakthroughChance(innovation);
        return new ProjectDraw(rng, noise, failed, breakthrough, innovation);
    }

    private int InnovationOf(DevProject project)
    {
        foreach (var engineer in EngineerRoster.Of(_inputs.World, _organization, Today))
        {
            if (engineer.Id == project.Engineer)
            {
                return engineer.Innovation;
            }
        }

        return CarEstimates.DefaultStaffAttribute;
    }

    /// <summary>
    /// A finished (or cut) new concept: its ceiling is drawn against the ceiling of the concept in the car, from the character the
    /// principal chose, the design staff and the lead's innovation. It is then ready, and the principal decides when it goes in.
    /// </summary>
    private void FinishRedesign(DevProject project, ProjectDraw draw, double fraction, bool cut)
    {
        if (draw.Failed && !cut)
        {
            Replace(project with { OutcomeMilli = 0, Status = ProjectStatus.Failed, ClosedOn = Today });
            return;
        }

        var reference = Cars.Of(_organization).FirstOrDefault();
        var anchor = reference?.ConceptCeiling ?? 0d;
        var quality = TeamEngineers.ExecutionQuality(_inputs.World, _organization, Today);
        var breakthrough = draw.Breakthrough && !cut;
        var ceiling = DevelopmentMath.DrawCeiling(draw.Rng, anchor, project.PhilosophyMilli, quality, draw.Innovation, breakthrough, fraction);
        Replace(project with
        {
            OutcomeMilli = DevelopmentEstimates.Milli(fraction),
            CeilingMilli = DevelopmentEstimates.Milli(ceiling),
            Flags = project.Flags | (breakthrough ? ProjectFlags.Breakthrough : 0),
            Status = ProjectStatus.Ready,
            ClosedOn = null,
        });
    }

    /// <summary>Applies a finished (or cut) project with its realised share.</summary>
    private void Finish(DevProject project, double share, bool failed, bool cut, bool breakthrough)
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

                var lucky = breakthrough && !failed && !cut && share > 0d;
                if (lucky)
                {
                    share = Math.Min(1d, share * DevelopmentEstimates.BreakthroughShareMultiple);
                    outcome = DevelopmentEstimates.Milli(share);
                }

                var area = project.Area ?? throw new InvalidOperationException("An upgrade has an area.");
                double? understandingBefore = null;
                var understandingAfter = 0d;
                foreach (var car in Cars.Of(_organization))
                {
                    var lifted = lucky
                        ? car.WithDesign(
                            car.Season,
                            car.Concept,
                            car.Levels,
                            Math.Min(100d, car.ConceptCeiling + DevelopmentEstimates.BreakthroughUpgradeLift),
                            car.Understanding,
                            car.TyreWearMultiplier,
                            car.SupplierChangeCost)
                        : car;
                    var gain = DevelopmentMath.Gain(DevelopmentMath.Headroom(lifted, area), share);
                    var levels = DevelopmentMath.WithLevel(lifted.Levels, area, DevelopmentMath.LevelOf(lifted.Levels, area) + gain);
                    var understanding = CarEstimates.Quantize(Math.Max(0d, car.Understanding - (DevelopmentEstimates.UpgradeUnderstandingHit * share)));
                    understandingBefore ??= car.Understanding;
                    understandingAfter = understanding;
                    Cars = Cars.Replace(DevelopmentEngine.Restamp(lifted, levels, understanding));
                }

                Note(UnderstandingSources.Part, (understandingBefore ?? understandingAfter) - understandingAfter);
                Replace(project with
                {
                    OutcomeMilli = outcome,
                    Status = failed ? ProjectStatus.Failed : cut ? ProjectStatus.Cut : ProjectStatus.Completed,
                    ClosedOn = Today,
                    Flags = project.Flags | (lucky ? ProjectFlags.Breakthrough : 0),
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

    /// <summary>
    /// Two streams of work (PP-066). The parts stream is the engineers' own: they pick the next upgrade, up to the slots the headcount
    /// gives. The concept stream is the next car: one concept at a time, built by the share of people the principal set, and only when
    /// no concept is already waiting or being built.
    /// </summary>
    private void StartProjects()
    {
        var annual = DevelopmentMath.AnnualBudgetCents(Finance.TypicalCents);
        var cars = Cars.Of(_organization);
        if (annual <= 0d || cars.Count == 0)
        {
            return;
        }

        var open = Projects();
        var conceptBusy = open.Any(project => project.Kind == DevKind.Concept && project.Status is ProjectStatus.Active or ProjectStatus.Ready or ProjectStatus.InProduction);
        var wantConcept = !conceptBusy && _plan.PercentOf(DevKind.Concept) > 0;
        var running = open.Where(project => project.IsActive && project.Kind != DevKind.Concept).ToList();
        if (running.Count >= DevelopmentEstimates.MaxSlots && !wantConcept)
        {
            return;
        }

        var engineers = EngineerRoster.Of(_inputs.World, _organization, Today);
        var balance = Finance.HasBook(_organization) ? Finance.BalanceOf(_organization) : 0L;
        var capacity = InfrastructureEffect.Apply(
            EngineeringCapacity.Derive(engineers, Today.Year, EngineerRoster.ChairsIn(Today.Year), balance, (long)annual),
            _inputs.World,
            _organization,
            Today.Year);
        if (wantConcept)
        {
            StartConcept(annual, cars, engineers, capacity, running);
        }

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
                slot,
                ProjectStream.Parts);
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

    /// <summary>
    /// The next concept: the engineers pick who leads it, the principal's character (philosophy and aero) is written into the
    /// project, and the crew the slider gives decides how long it takes (twice the share, a shorter design; ESTIMATE).
    /// </summary>
    private void StartConcept(
        double annual,
        IReadOnlyList<TeamCar> cars,
        IReadOnlyList<Engineer> engineers,
        EngineeringCapacity capacity,
        List<DevProject> running)
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
            DevelopmentEstimates.MaxSlots,
            ProjectStream.Concept);
        if (decision is null)
        {
            return;
        }

        var chosen = decision.Chosen;
        var percent = _plan.PercentOf(DevKind.Concept);
        var cost = DevelopmentMath.CostCents(annual, percent, DevKind.Concept, Today.Year);
        if (cost <= 0)
        {
            return;
        }

        var crew = Math.Clamp(Math.Sqrt((double)DevelopmentEstimates.DefaultNextYearPercent / percent), DevelopmentEstimates.DurationFloor, DevelopmentEstimates.DurationCeiling);
        var days = Math.Max(1, (int)Math.Round(capacity.DurationDays(DevelopmentMath.BaseDays(DevKind.Concept, Today.Year)) * crew, MidpointRounding.AwayFromZero));
        var risk = DevelopmentMath.ConceptRisk(chosen.Skill, Today.Year, _plan.NextPhilosophyMilli);
        var project = new DevProject(
            Development.NextProject,
            _organization,
            DevKind.Concept,
            null,
            chosen.Engineer.Id,
            Today,
            days,
            0,
            cost,
            0,
            0,
            DevelopmentEstimates.Milli(risk),
            null,
            ProjectStatus.Active,
            ConceptTiming.Hold,
            0,
            0,
            null,
            PhilosophyMilli: _plan.NextPhilosophyMilli,
            AeroMilli: _plan.NextAeroMilli,
            Flags: ProjectFlags.Redesign);
        Development = Development.AddProject(project);
        _plan = _plan.WithSpent(DevKind.Concept, cost);
        Changed = true;
        if (_inputs.Trace.IsEnabled)
        {
            _inputs.Trace.Record(EngineerChoice.Trace(_organization, decision, Today, "development.choose"));
        }
    }

    private void GrowUnderstanding()
    {
        var active = Projects().Count(project => project.IsActive);
        var points = (DevelopmentEstimates.UnderstandingPerDay + (DevelopmentEstimates.UnderstandingPerActiveProject * active))
            * InfrastructureEffect.Factors(_inputs.World, _organization, Today.Year).UnderstandingScale;
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

    /// <summary>Records where the car's understanding moved, for the Auto screen. <paramref name="points"/> is the drop (positive) in understanding points.</summary>
    private void Note(string source, double points)
    {
        var milli = -DevelopmentEstimates.Milli(points);
        if (milli == 0)
        {
            return;
        }

        Development = Development.AddNote(new UnderstandingNote(_organization, Today, source, milli));
        Changed = true;
    }

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
