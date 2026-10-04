using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Contracts;
using Paddock.Domain.Principals;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;

namespace Paddock.Application.Principals;

/// <summary>
/// Which organization an AI principal's manager runs, for the modules that look a manager up by id (the talent pool, the objectives). The AI
/// manager of a team is <c>ai:{organizationId}</c>; any other manager is looked up in the host's own table.
/// </summary>
public sealed class PrincipalOrganizations : IManagerOrganizations
{
    private readonly IManagerOrganizations? _others;

    public PrincipalOrganizations(IManagerOrganizations? others = null)
    {
        _others = others;
    }

    public OrganizationId? OrganizationOf(string managerId)
    {
        ArgumentNullException.ThrowIfNull(managerId);
        if (managerId.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal)
            && Paddock.Application.Finance.FinanceIds.TryParse(managerId[PrincipalKeys.ManagerPrefix.Length..], out var organization))
        {
            return organization;
        }

        return _others?.OrganizationOf(managerId);
    }
}

/// <summary>
/// The AI team principals (T44, DESIGN section 8): once a morning it looks at every AI-run team whose review is due, decides with the pure
/// deciders of <c>Paddock.Simulation.Ai</c> from what the team's own manager could know, and files the decisions as the same commands a
/// player files, under the manager id of the team's AI (<see cref="PrincipalKeys.ManagerOf"/>), into the normal queue. It changes nothing
/// itself (INV-001): even its own record of a review is a command (<see cref="RecordPrincipalReviewCommand"/>).
/// <para>
/// <b>Never for a human.</b> A team any human manager of the <see cref="ManagerRegistry"/> runs is skipped, and the command handler refuses
/// the record for it.
/// </para>
/// <para>
/// <b>Cadence.</b> A review computes when the team next needs its principal (<see cref="ReviewSchedule"/>): a contract window, a talk's
/// answer, a sponsor's terms, the next season, or the scheduled date. Until then the team costs one date comparison a morning. The host
/// can pull a team forward with <see cref="Raise"/> (a race result, a regulation change, a retirement). A raised review is held in memory
/// for the next morning only; it is a hint, not saved state.
/// </para>
/// <para>
/// <b>RNG (INV-004).</b> The noise of a choice and the tie between equal ones come from the <c>AiDecisions</c> stream derived from the master
/// seed and the season, by child streams keyed by team, decision and option; no stream is advanced, so nothing else shifts.
/// </para>
/// </summary>
public sealed class PrincipalDirector
{
    /// <summary>ESTIMATE: reviews on consecutive days that file commands before the principal waits <see cref="AiEstimates.VacancyRetryDays"/> (stops a rejected command from being retried daily).</summary>
    public const int MaxFollowUps = 3;

    private readonly PrincipalEnvironment _env;
    private readonly HashSet<string> _raised = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _streaks = new(StringComparer.Ordinal);
    private bool _raisedAll;

    public PrincipalDirector(PrincipalEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _env = environment;
    }

    /// <summary>
    /// Pulls the next review of a team, or of every team when <paramref name="organization"/> is null, to the next morning. The trigger names
    /// why (it is written to nothing; the decisions are traced by what they decided). A hint kept in memory only.
    /// </summary>
    public void Raise(ReviewTrigger trigger, OrganizationId? organization = null)
    {
        _ = trigger;
        if (organization is OrganizationId one)
        {
            _raised.Add(one.Value);
        }
        else
        {
            _raisedAll = true;
        }
    }

    /// <summary>
    /// The morning step: files the commands of every AI team that is due, and returns how many it filed (the record commands included).
    /// The host calls it each morning after its books are synced to the world and before it drains the queue.
    /// </summary>
    public int FileCommands(CommandQueue queue, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var world = _env.ReadWorld();
        var section = world.Section<PrincipalsSection>(PrincipalsSection.SectionName) ?? PrincipalsSection.Empty;
        var filed = 0;
        foreach (var team in world.Organizations)
        {
            if (team.Kind != OrganizationKind.Team || team.Dissolved is not null)
            {
                continue;
            }

            var record = section.Of(team.Id);
            var due = record is null || record.NextReview <= today || _raisedAll || _raised.Contains(team.Id.Value);
            if (!due || IsHumanRun(team.Id))
            {
                continue;
            }

            EnsureManager(team.Id);
            filed += Review(queue, world, team.Id, record, today);
        }

        _raised.Clear();
        _raisedAll = false;
        return filed;
    }

    private bool IsHumanRun(OrganizationId organization)
    {
        foreach (var control in _env.Controls)
        {
            foreach (var manager in control.ManagersOf(organization))
            {
                if (_env.Managers.Contains(manager) && _env.Managers.KindOf(manager) == ManagerKind.Human)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void EnsureManager(OrganizationId organization)
    {
        var manager = PrincipalKeys.ManagerOf(organization);
        if (!_env.Managers.Contains(manager))
        {
            _env.Managers.Register(manager, ManagerKind.Ai, "AI " + organization.Value);
        }

        foreach (var control in _env.Controls)
        {
            control.Assign(manager, organization);
        }
    }

    private int Review(CommandQueue queue, WorldState world, OrganizationId organization, AiPrincipalRecord? record, GameDate today)
    {
        var knowledge = new TeamKnowledge(_env, world, organization, today);
        var principal = knowledge.PrincipalPerson();
        var archetype = ArchetypeFor(world, organization, record, principal, knowledge, today);
        var skills = PrincipalLevels.From(knowledge.PrincipalBeliefs(principal));
        var context = new DecisionContext(
            organization.Value,
            knowledge.Manager.Value,
            knowledge.Day,
            archetype,
            skills,
            AiRandom.ForSeason(_env.MasterSeed, today.Year),
            _env.Bias,
            _env.Trace);

        var filer = new CommandFiler(queue, knowledge);
        var events = new List<DateOnly>();
        var sacrificed = record?.SacrificedSeason ?? 0;
        var scoutSeason = record?.ScoutSeason ?? 0;
        var vacancyOpen = false;
        var sponsorTalkOpen = false;
        var roles = record?.StaffRoles ?? string.Empty;

        if (knowledge.Market(record) is { } market)
        {
            var actions = MarketDecider.Review(context, market.Input);
            filer.Market(_env.Contracts!, market, actions);
            events.AddRange(market.Events);
            roles = string.Join(',', market.StaffRoles.Order(StringComparer.Ordinal));
            vacancyOpen = StillVacant(market.Input, actions);
        }

        if (knowledge.Development(record) is { } development)
        {
            var outcome = DevelopmentDecider.Review(context, development);
            filer.Development(outcome);
            if (outcome.Sacrifice)
            {
                sacrificed = today.Year;
            }
        }

        if (knowledge.Supply() is { } supply)
        {
            var actions = SupplyDecider.Review(context, supply.Input);
            filer.Supply(_env.Supply!, actions);
            events.AddRange(supply.Events);
        }

        if (knowledge.Sponsors() is { } sponsors)
        {
            var actions = SponsorDecider.Review(context, sponsors.Input);
            filer.Sponsors(_env.Sponsors!, actions);
            events.AddRange(sponsors.Events);
            sponsorTalkOpen = sponsors.TalkOpen;
        }

        var poolSize = knowledge.PoolSize();
        if (poolSize > 0 && scoutSeason < today.Year)
        {
            if (ScoutingDecider.Review(context, new ScoutingInput(knowledge.Day, scoutSeason, poolSize)))
            {
                filer.ScoutPool();
            }

            scoutSeason = today.Year;
        }

        var key = organization.Value;
        var follow = filer.Filed > 0;
        _streaks.TryGetValue(key, out var streak);
        if (follow && streak >= MaxFollowUps)
        {
            follow = false;
        }

        _streaks[key] = filer.Filed > 0 ? streak + 1 : 0;
        var next = ReviewSchedule.Next(knowledge.Day, follow, vacancyOpen, sponsorTalkOpen, events);
        filer.RecordReview(archetype, principal?.Id, next, sacrificed, scoutSeason, roles);
        return filer.Filed + 1;
    }

    /// <summary>True when, after the actions of this review, a vacancy is left that the principal could not start a talk for.</summary>
    private static bool StillVacant(MarketInput input, IReadOnlyList<MarketAction> actions)
    {
        foreach (var vacancy in input.Vacancies)
        {
            var approached = actions.OfType<MarketAction.Approach>().Count(action => action.Subject == vacancy.Subject);
            if (approached < vacancy.Count)
            {
                return true;
            }
        }

        return false;
    }

    private PrincipalArchetype ArchetypeFor(
        WorldState world,
        OrganizationId organization,
        AiPrincipalRecord? record,
        Person? principal,
        TeamKnowledge knowledge,
        GameDate today)
    {
        if (record is not null && record.Person == principal?.Id && PrincipalArchetypes.TryParse(record.Archetype, out var kept))
        {
            return kept;
        }

        // A new team, or a new principal: the style is assigned again, from public facts (tier, age, recent results).
        var teams = world.Organizations.Where(item => item.Kind == OrganizationKind.Team && item.Dissolved is null).ToArray();
        var own = world.GetOrganization(organization).Budget;
        var below = teams.Count(item => item.Budget < own);
        var equal = teams.Count(item => item.Budget == own);
        var tier = teams.Length <= 1 ? 0.5 : (below + (equal - 1) / 2.0) / (teams.Length - 1);
        var positions = new List<int>();
        for (var back = 1; back <= 3; back++)
        {
            if (knowledge.Position(today.Year - back) is int position)
            {
                positions.Add(position);
            }
        }

        var age = principal is null ? (int?)null : today.Year - principal.BirthDate.Year;
        return ArchetypeAssignment.Assign(
            AiRandom.ForSeason(_env.MasterSeed, today.Year),
            organization.Value,
            new ArchetypeInput(tier, age, positions, teams.Length));
    }
}

/// <summary>Turns decisions into the commands a player would file. Every command names the AI manager of the team and goes into the queue.</summary>
internal sealed class CommandFiler
{
    private readonly CommandQueue _queue;
    private readonly TeamKnowledge _knowledge;

    public CommandFiler(CommandQueue queue, TeamKnowledge knowledge)
    {
        _queue = queue;
        _knowledge = knowledge;
    }

    /// <summary>The commands filed so far, not counting the review record.</summary>
    public int Filed { get; private set; }

    private Paddock.Application.Managers.ManagerId Manager => _knowledge.Manager;

    private DateOnly Day => _knowledge.Day;

    private string Organization => _knowledge.Organization.Value;

    private void File(ICommand command)
    {
        _queue.Enqueue(command);
        Filed++;
    }

    public void RecordReview(PrincipalArchetype archetype, PersonId? person, DateOnly next, int sacrificed, int scoutSeason, string roles) =>
        _queue.Enqueue(new RecordPrincipalReviewCommand
        {
            ManagerId = Manager,
            IssuedOn = Day,
            OrganizationId = Organization,
            Archetype = PrincipalArchetypes.Text(archetype),
            Person = person?.Value ?? string.Empty,
            NextReview = next,
            SacrificedSeason = sacrificed,
            ScoutSeason = scoutSeason,
            StaffRoles = roles,
        });

    // ---------------------------------------------------------------- market

    public void Market(ContractEngine engine, MarketBundle bundle, IReadOnlyList<MarketAction> actions)
    {
        var today = _knowledge.Today;
        foreach (var action in actions)
        {
            switch (action)
            {
                case MarketAction.Renew renew:
                    if (bundle.Contracts.TryGetValue(renew.ContractId, out var contract))
                    {
                        var terms = Terms(renew.Offer);
                        if (engine.ValidateRenew(Manager, contract.Id, false, terms, null, today) is null)
                        {
                            File(new RenewContractCommand { ManagerId = Manager, IssuedOn = Day, Contract = contract.Id, Offer = terms });
                        }
                    }

                    break;
                case MarketAction.Approach approach:
                    if (bundle.Persons.TryGetValue(approach.PersonId, out var person))
                    {
                        File(new OpenNegotiationCommand
                        {
                            ManagerId = Manager,
                            IssuedOn = Day,
                            Organization = _knowledge.Organization,
                            Person = person,
                            Subject = NegotiationSubject.Parse(approach.Subject),
                        });
                    }

                    break;
                case MarketAction.SubmitOffer submit:
                    var offer = Terms(submit.Offer);
                    if (engine.ValidateSubmit(Manager, submit.NegotiationId, offer, today) is null)
                    {
                        File(new SubmitOfferCommand { ManagerId = Manager, IssuedOn = Day, NegotiationId = submit.NegotiationId, Terms = offer });
                    }
                    else
                    {
                        WalkAway(engine, submit.NegotiationId, today);
                    }

                    break;
                case MarketAction.Accept accept:
                    if (engine.ValidateAccept(Manager, accept.NegotiationId, today) is null)
                    {
                        File(new AcceptCounterOfferCommand { ManagerId = Manager, IssuedOn = Day, NegotiationId = accept.NegotiationId });
                    }
                    else
                    {
                        WalkAway(engine, accept.NegotiationId, today);
                    }

                    break;
                case MarketAction.WalkAway walk:
                    WalkAway(engine, walk.NegotiationId, today);
                    break;
            }
        }
    }

    private void WalkAway(ContractEngine engine, string negotiationId, GameDate today)
    {
        _ = today;
        if (engine.ValidateWalk(Manager, negotiationId) is null)
        {
            File(new WalkAwayCommand { ManagerId = Manager, IssuedOn = Day, NegotiationId = negotiationId });
        }
    }

    private static OfferTerms Terms(AiOffer offer) =>
        new(
            offer.SalaryDollars,
            0,
            0,
            0,
            Math.Clamp(offer.Years, 1, NegotiationEstimates.MaxYears),
            offer.Seat is null ? null : Enum.Parse<SeatStatus>(offer.Seat),
            null,
            null);

    // ---------------------------------------------------------------- development

    public void Development(DevelopmentDecision outcome)
    {
        if (outcome.Plan is { } plan)
        {
            File(new SetDevelopmentSplitCommand
            {
                ManagerId = Manager,
                IssuedOn = Day,
                OrganizationId = Organization,
                CurrentPercent = plan.Current,
                AccountPercent = plan.Account,
                NextYearPercent = plan.NextYear,
                AeroPriority = plan.Aero,
                ChassisPriority = plan.Chassis,
                ReliabilityPriority = plan.Reliability,
                TyresPriority = plan.Tyres,
            });
        }

        foreach (var timing in outcome.Timings)
        {
            if (timing.Commit)
            {
                File(new CommitConceptCommand { ManagerId = Manager, IssuedOn = Day, OrganizationId = Organization, ProjectId = timing.ProjectId });
                continue;
            }

            File(new DeployConceptCommand
            {
                ManagerId = Manager,
                IssuedOn = Day,
                OrganizationId = Organization,
                ProjectId = timing.ProjectId,
                Timing = timing.Timing,
                Races = timing.Races,
            });
        }
    }

    // ---------------------------------------------------------------- supply

    public void Supply(SupplySources sources, IReadOnlyList<SupplyAction> actions)
    {
        var today = _knowledge.Today;
        foreach (var action in actions)
        {
            switch (action)
            {
                case SupplyAction.Propose propose:
                    if (!Enum.TryParse<SupplyKind>(propose.Kind, out var kind) || !Enum.TryParse<SupplyItem>(propose.Item, out var item)
                        || !Paddock.Application.Finance.FinanceIds.TryParse(propose.SupplierId, out var supplier))
                    {
                        break;
                    }

                    var revising = string.IsNullOrEmpty(propose.NegotiationId) ? null : propose.NegotiationId;
                    if (SupplyRules.CheckProposal(sources.Book, sources.Environment, _knowledge.Organization, supplier, item, kind, propose.FirstSeason, propose.PriceCents, propose.Seasons, today, revising) is null)
                    {
                        File(new ProposeSupplyDealCommand
                        {
                            ManagerId = Manager,
                            IssuedOn = Day,
                            OrganizationId = Organization,
                            SupplierId = propose.SupplierId,
                            Item = item,
                            Kind = kind,
                            FirstSeason = propose.FirstSeason,
                            AnnualPriceCents = propose.PriceCents,
                            Seasons = propose.Seasons,
                            NegotiationId = propose.NegotiationId,
                        });
                    }

                    break;
                case SupplyAction.Respond respond:
                    File(new RespondToSupplyOfferCommand
                    {
                        ManagerId = Manager,
                        IssuedOn = Day,
                        OrganizationId = Organization,
                        NegotiationId = respond.NegotiationId,
                        Accept = respond.Accept,
                    });
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- sponsors

    public void Sponsors(SponsorSources sources, IReadOnlyList<SponsorAction> actions)
    {
        var today = _knowledge.Today;
        foreach (var action in actions)
        {
            switch (action)
            {
                case SponsorAction.BeginTalks begin:
                    if (SponsorRules.CanBegin(sources.Book, sources.Environment, _knowledge.Organization, begin.SponsorId, begin.Slot, today) is null)
                    {
                        File(new BeginSponsorTalksCommand
                        {
                            ManagerId = Manager,
                            IssuedOn = Day,
                            OrganizationId = Organization,
                            SponsorId = begin.SponsorId,
                            Slot = begin.Slot,
                        });
                    }

                    break;
                case SponsorAction.SignAtCurrentTerms sign:
                    File(new SignAtCurrentTermsCommand { ManagerId = Manager, IssuedOn = Day, OrganizationId = Organization, TalkId = sign.TalkId });
                    break;
                case SponsorAction.RespondToOffer respond:
                    File(new RespondToSponsorOfferCommand
                    {
                        ManagerId = Manager,
                        IssuedOn = Day,
                        OrganizationId = Organization,
                        OfferId = respond.OfferId,
                        Accept = respond.Accept,
                    });
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- scouting

    public void ScoutPool() =>
        File(new AssignScoutFocusCommand { ManagerId = Manager, IssuedOn = Day, PersonHandle = null });
}

/// <summary>
/// Wires the AI team principals into a career. One call registers the handler of the principals' own command and returns the director the
/// host calls each morning; it does not touch the career loop. The host also assigns nothing: the director registers the AI manager of
/// each AI-run team and assigns it in every control table of the environment as it first reviews the team.
/// </summary>
public static class PrincipalRegistration
{
    public static PrincipalDirector Register(CommandDispatcher dispatcher, PrincipalsBook book, PrincipalEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        dispatcher.Register(new RecordPrincipalReviewHandler(book, environment.PrimaryControl));
        return new PrincipalDirector(environment);
    }
}
