using System.Globalization;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Board;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Principals;
using Paddock.Domain.Racing;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Board;

/// <summary>
/// The rules of the board: who runs which team, what the board expects, how its confidence moves, when it dismisses, who replaces
/// a dismissed principal, and the job offers a dismissed manager gets (T45, PP-007, PP-050). Shared by the commands, the inbox
/// resolver and the day handler. A validation returns the reason something cannot be done, or null, and changes nothing and draws
/// no RNG (INV-005). A change happens only inside a command or a day tick (INV-001).
/// <para>
/// <b>RNG (INV-004).</b> The only draws are the choice of a replacement principal (and its new archetype) and the choice of an
/// ordinary job offer. Both come from the <see cref="RngStreamName.Market"/> stream of the season, taken as a child keyed by the
/// purpose, the organization and the date, so a draw never depends on how many other draws happened and a command can make one
/// without a day context. The board's own decisions (confidence, dismissal) are deterministic: no noise, so
/// <see cref="RngStreamName.AiDecisions"/> is not used.
/// </para>
/// <para>
/// Every number is an ESTIMATE (<see cref="BoardEstimates"/>). The set of archetypes below is a stand-in until the AI principals
/// (T44) own the real one.
/// </para>
/// </summary>
public sealed class BoardEngine
{
    /// <summary>Inbox kind of a job offer to a manager without a team.</summary>
    public const string OfferKind = "board.jobOffer";

    /// <summary>Inbox kind of an information item from the board.</summary>
    public const string NoticeKind = "board.notice";

    /// <summary>Argument name that ties an offer to the organization that makes it.</summary>
    public const string OrganizationArgument = "organization";

    public const string OptionAccept = "accept";

    public const string OptionDecline = "decline";

    /// <summary>Inbox kind of the season-target decision offered to a human principal.</summary>
    public const string SeasonTargetKind = "board.seasonTarget";

    /// <summary>ESTIMATE: the archetype of a board that has not rerolled one.</summary>
    public const string DefaultArchetype = "balanced";

    /// <summary>ESTIMATE: stand-in archetypes of an AI principal. A replacement rerolls to a different one, so the team changes direction.</summary>
    public static readonly IReadOnlyList<string> Archetypes = ["balanced", "conservative", "aggressive", "developer", "financial"];

    private const int TraceLevel = 3;

    private readonly BoardBook _book;
    private readonly InboxBook _inbox;
    private readonly ManagerRegistry _managers;

    public BoardEngine(BoardBook book, InboxBook inbox, ManagerRegistry managers)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(managers);
        _book = book;
        _inbox = inbox;
        _managers = managers;
    }

    public BoardBook Book => _book;

    /// <summary>The engine for the board, inbox and managers of a command context.</summary>
    public static BoardEngine From(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new BoardEngine(context.Board, context.Inbox, context.Managers);
    }

    // ---------------------------------------------------------------- validation

    /// <summary>Why a manager cannot take the job an offer item names, or null.</summary>
    public TranslationMessage? ValidateAcceptOffer(ManagerId manager, InboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_book.Section.UnemployedManager(manager.Value) is null)
        {
            return TranslationMessage.Of(BoardKeys.NotUnemployed);
        }

        var organization = OfferedOrganization(item);
        var board = organization is OrganizationId id ? _book.Section.Board(id) : null;
        return board?.Principal is { Kind: PrincipalKind.Ai } ? null : TranslationMessage.Of(BoardKeys.OfferGone);
    }

    public TranslationMessage? ValidateResign(ManagerId manager)
    {
        if (_book.Section.OrganizationOf(manager.Value) is not OrganizationId organization)
        {
            return TranslationMessage.Of(BoardKeys.NotEmployed);
        }

        return _book.Section.Board(organization)?.Principal is { Founder: true }
            ? TranslationMessage.Of(BoardKeys.FounderCannotResign)
            : null;
    }

    // ---------------------------------------------------------------- changes a manager causes

    /// <summary>
    /// Gives a team to a manager: the AI principal goes, the board starts again at its initial confidence, and the new principal is
    /// protected for the first full season and longer with reputation (PP-050). Used when a job offer is taken and when a career
    /// starts. The caller has validated.
    /// </summary>
    public IReadOnlyList<BoardFact> AppointHuman(ManagerId manager, OrganizationId organization, GameDate today, bool founder, string reasonKey)
    {
        var facts = new List<BoardFact>();
        var board = _book.Section.Board(organization) ?? throw new InvalidOperationException($"Organization '{organization}' has no board.");
        var outgoing = board.Principal;
        if (outgoing is { Kind: PrincipalKind.Ai })
        {
            EndPrincipalContracts(PersonOf(outgoing.Subject), organization, today);
        }

        var section = _book.Section.WithInitialReputation(manager.Value, BoardEstimates.InitialReputationTenths).WithoutUnemployed(manager.Value);
        var reputation = section.ReputationTenths(manager.Value);
        var principal = new PrincipalRecord(PrincipalKind.Human, manager.Value, today, ReputationModel.ProtectedUntil(today, reputation), founder);
        var alreadyRaced = SeasonAlreadyRaced(today)
            || (board.LastReview is GameDate reviewed && reviewed.Year == today.Year);
        _book.Update(section.WithBoard(board with
        {
            Principal = principal,
            ConfidenceTenths = BoardEstimates.InitialConfidenceTenths,
            LowStreak = 0,
            LastReview = null,
            LastTargetTenths = null,
        }));
        facts.Add(new BoardFact(
            BoardEventTypes.ManagerAppointed,
            new BoardFactPayload(organization.Value, manager.Value, PrincipalKind.Human.ToString(), outgoing?.Subject ?? string.Empty, string.Empty, reasonKey, 0)));
        OfferSeasonTargetOnAppointment(organization, manager, today, alreadyRaced);
        return facts;
    }

    /// <summary>
    /// A human who takes the seat before the first race of the season is offered the three season targets. An objective the
    /// previous principal was already given is withdrawn with no effect. After a race has been run, the objective stays.
    /// </summary>
    private void OfferSeasonTargetOnAppointment(OrganizationId organization, ManagerId manager, GameDate today, bool alreadyRaced)
    {
        if (alreadyRaced)
        {
            return;
        }

        foreach (var objective in _book.Objectives.OwnedBy(organization))
        {
            if (objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason && objective.Deadline.Year == today.Year)
            {
                _book.Update(_book.Objectives.Withdraw(objective.Id, today));
            }
        }

        if (TargetPending(manager.Value, organization))
        {
            return;
        }

        var fieldSize = ActiveTeams(today).Count;
        var publicExpected = ReputationModel.ExpectedPosition(
            _book.History.FinalPosition(organization, today.Year - 1),
            BudgetRank(organization, today),
            fieldSize);
        PostSeasonTarget(manager.Value, organization, publicExpected, fieldSize, today);
        var board = _book.Section.Board(organization);
        if (board is not null)
        {
            _book.Update(_book.Section.WithBoard(board with { ExpectedPosition = publicExpected }));
        }
    }

    private bool SeasonAlreadyRaced(GameDate today)
    {
        var championship = _book.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        return championship is not null && championship.Season == today.Year && championship.RoundsCompleted > 0;
    }

    /// <summary>
    /// Why a manager cannot take over <paramref name="organizationId"/>, or null. Founding (<see cref="CareerStartPath"/>)
    /// is refused. The check changes nothing.
    /// </summary>
    public TranslationMessage? ValidateTakeOver(
        ManagerId manager,
        string organizationId,
        string givenName,
        string familyName,
        string nationality,
        string tilt)
    {
        if (CareerStartPath.IsFounding(organizationId))
        {
            return TranslationMessage.Of(BoardKeys.TakeOverOwnTeam);
        }

        if (!PlayerPrincipal.TryTilt(tilt, out _))
        {
            return TranslationMessage.Of(BoardKeys.TakeOverBadTilt, ("tilt", tilt));
        }

        if (!TryOrganization(organizationId, out var organization, out var rejection))
        {
            return rejection;
        }

        if (!_managers.Contains(manager) || _managers.KindOf(manager) != ManagerKind.Human)
        {
            return TranslationMessage.Of(BoardKeys.TakeOverNotHuman);
        }

        if (_book.Section.OrganizationOf(manager.Value) is not null)
        {
            return TranslationMessage.Of(BoardKeys.TakeOverAlreadyEmployed);
        }

        var board = _book.Section.Board(organization);
        if (board is null)
        {
            return TranslationMessage.Of(BoardKeys.TakeOverNoBoard);
        }

        if (board.Principal is { Kind: PrincipalKind.Human })
        {
            return TranslationMessage.Of(BoardKeys.TakeOverAlreadyHuman);
        }

        try
        {
            _ = PlayerPrincipal.Spec(givenName, familyName, nationality, _book.World.CurrentDate.Year, null);
        }
        catch (ArgumentException)
        {
            return TranslationMessage.Of(BoardKeys.TakeOverBadName);
        }

        return null;
    }

    /// <summary>
    /// The manager becomes the human principal of an existing team and a person with principal attributes (ESTIMATE).
    /// The caller has validated. No organization is created.
    /// </summary>
    public IReadOnlyList<BoardFact> TakeOver(
        ManagerId manager,
        string organizationId,
        string givenName,
        string familyName,
        string nationality,
        string tilt,
        GameDate today)
    {
        if (!PlayerPrincipal.TryTilt(tilt, out var attribute) || !TryOrganization(organizationId, out var organization, out _))
        {
            throw new InvalidOperationException("Take over ran for a command that should have been rejected.");
        }

        var spec = PlayerPrincipal.Spec(givenName, familyName, nationality, today.Year, attribute);
        var (withPerson, person) = _book.World.AddPerson(spec);
        var believed = withPerson.SetKnowledge(PlayerPrincipal.Knowledge(organization, person, spec.Truth));
        _book.Contracts.Update(believed);
        var facts = AppointHuman(manager, organization, today, founder: false, BoardKeys.TakeOverReason).ToList();
        var end = GameDate.SeasonEnd(today.Year + 1);
        var (withContract, _) = _book.World.AddContract(new ContractSpec(
            person,
            organization,
            ContractRole.Staff(StaffRole.TeamPrincipal),
            today,
            end,
            salary: 0,
            exclusive: true,
            option: null,
            releaseClause: null));
        _book.Contracts.Update(withContract);
        return facts;
    }

    private bool TryOrganization(string organizationId, out OrganizationId organization, out TranslationMessage? rejection)
    {
        organization = default;
        rejection = null;
        try
        {
            organization = OrganizationId.Real(organizationId);
        }
        catch (ArgumentException)
        {
            rejection = TranslationMessage.Of(BoardKeys.TakeOverUnknownTeam, ("team", organizationId));
            return false;
        }

        var id = organization;
        var found = _book.World.Organizations.FirstOrDefault(candidate => candidate.Id == id);
        if (found is null)
        {
            rejection = TranslationMessage.Of(BoardKeys.TakeOverUnknownTeam, ("team", organizationId));
            return false;
        }

        if (found.Kind != OrganizationKind.Team || (found.Dissolved is GameDate dissolved && dissolved < _book.World.CurrentDate))
        {
            rejection = TranslationMessage.Of(BoardKeys.TakeOverNotATeam, ("team", organizationId));
            return false;
        }

        return true;
    }

    /// <summary>The manager leaves a team of their own accord: no severance, a small blow to reputation. The caller has validated.</summary>
    public IReadOnlyList<BoardFact> Resign(ManagerId manager, GameDate today)
    {
        var organization = _book.Section.OrganizationOf(manager.Value)
            ?? throw new InvalidOperationException("Resign ran for a command that should have been rejected.");
        var facts = new List<BoardFact>
        {
            Leave(manager, organization, today, 0, BoardKeys.ReputationResigned, BoardEstimates.ResignationTenths, BoardKeys.NoticeResigned, BoardEventTypes.ManagerResigned),
        };
        facts.AddRange(HireAiPrincipal(organization, today, null, BoardKeys.ReasonSeason));
        return facts;
    }

    /// <summary>The manager takes the job an offer names. Every other open offer is withdrawn. The caller has validated.</summary>
    public IReadOnlyList<BoardFact> AcceptOffer(ManagerId manager, InboxItem item, GameDate today)
    {
        var organization = OfferedOrganization(item) ?? throw new InvalidOperationException("Accept ran for an item that should have been rejected.");
        var facts = AppointHuman(manager, organization, today, false, BoardKeys.OfferSubject).ToList();
        foreach (var other in _inbox.Section.ItemsOf(manager.Value).Where(other => other.IsOpen && other.Kind == OfferKind && other.Id != item.Id))
        {
            _inbox.Withdraw(_managers, other.Id, today);
        }

        return facts;
    }

    // ---------------------------------------------------------------- the day

    /// <summary>
    /// Everything the board does on one day, in order: boards for teams that have none, vacancies filled, the season's objectives,
    /// the reputation of a season that ends, the review after a race, and the job offers. Returns what happened, in order.
    /// </summary>
    public IReadOnlyList<BoardFact> DailyWork(GameDate today, bool raceToday)
    {
        var facts = new List<BoardFact>();
        EnsureBoards(today);
        foreach (var board in _book.Section.Boards.Where(board => board.Principal is null).ToArray())
        {
            facts.AddRange(HireAiPrincipal(board.Organization, today, null, BoardKeys.ReasonSeason));
        }

        GrantObjectives(today);
        if (today.IsSeasonEnd)
        {
            SettleSeasonReputation(today);
        }

        if (raceToday)
        {
            facts.AddRange(ReviewBoards(today));
        }

        facts.AddRange(OfferJobs(today));
        return facts;
    }

    /// <summary>Gives every active team a board with the patience its profile suggests. A team that has one is left alone.</summary>
    public void EnsureBoards(GameDate today)
    {
        foreach (var team in ActiveTeams(today))
        {
            if (_book.Section.Board(team.Id) is not null)
            {
                continue;
            }

            PrincipalRecord? principal = null;
            var contract = _book.World.Contracts
                .Where(candidate => candidate.OrganizationId == team.Id
                    && candidate.IsActiveOn(today)
                    && candidate.Role.IsStaff
                    && candidate.Role.StaffRole == StaffRole.TeamPrincipal)
                .OrderBy(candidate => candidate.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (contract is not null)
            {
                // A principal who was already there is not new, so nothing protects them.
                principal = new PrincipalRecord(PrincipalKind.Ai, contract.PersonId.Value, contract.Start, contract.Start, false);
                _book.Update(_book.Section.WithInitialReputation(contract.PersonId.Value, BoardEstimates.InitialReputationTenths));
            }

            _book.Update(_book.Section.WithBoard(new BoardRecord(
                team.Id,
                ReputationModel.PatienceFor(team.Founded, today),
                BoardEstimates.InitialConfidenceTenths,
                0,
                0,
                DefaultArchetype,
                principal,
                null,
                null,
                null)));
        }
    }

    /// <summary>
    /// Grants the season objective (and, when none is running, the multi-year one) to every board, from public facts only: the
    /// last position and the budget rank. They are ordinary objectives (T36), evaluated on their deadline.
    /// </summary>
    public void GrantObjectives(GameDate today)
    {
        var fieldSize = ActiveTeams(today).Count;
        foreach (var board in _book.Section.Boards.ToArray())
        {
            var owned = _book.Objectives.OwnedBy(board.Organization);
            var seasonOpen = owned.Any(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
            var multiOpen = owned.Any(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveMultiYear);
            if (seasonOpen && multiOpen)
            {
                continue;
            }

            var expected = seasonOpen && board.ExpectedPosition > 0
                ? board.ExpectedPosition
                : ReputationModel.ExpectedPosition(
                    _book.History.FinalPosition(board.Organization, today.Year - 1),
                    BudgetRank(board.Organization, today),
                    fieldSize);
            var publicExpected = expected;
            var baseline = _book.Facts.Number(board.Organization, ObjectiveFactKeys.ChampionshipPosition) ?? publicExpected;
            if (!seasonOpen)
            {
                if (board.Principal is { Kind: PrincipalKind.Human } human)
                {
                    if (!TargetPending(human.Subject, board.Organization))
                    {
                        PostSeasonTarget(human.Subject, board.Organization, publicExpected, fieldSize, today);
                    }
                }
                else
                {
                    var ambition = SeasonTarget.ForArchetype(StyleOf(board.Organization));
                    GrantSeason(board.Organization, publicExpected, fieldSize, ambition, baseline, today);
                    TraceSeasonTarget(board, ambition, today);
                    expected = SeasonTarget.Position(publicExpected, fieldSize, ambition);
                }
            }

            if (!multiOpen)
            {
                var target = Math.Max(1, publicExpected - BoardEstimates.MultiYearPlacesBetter);
                Grant(
                    board.Organization,
                    BoardKeys.ObjectiveMultiYear,
                    BoardKeys.ReasonMultiYear,
                    target,
                    baseline,
                    GameDate.SeasonEnd(today.Year + BoardEstimates.MultiYearSeasons - 1),
                    BoardEstimates.MultiYearObjectiveTenths,
                    today);
            }

            _book.Update(_book.Section.WithBoard(_book.Section.Board(board.Organization)! with { ExpectedPosition = expected }));
        }
    }

    /// <summary>
    /// The review after a race: confidence closes part of the gap to what the position against expectation deserves, then cash takes
    /// its toll. A principal who is not protected and whose confidence stays below the board's bar for enough consecutive reviews is
    /// dismissed. Returns the facts of any dismissal and replacement.
    /// </summary>
    public IReadOnlyList<BoardFact> ReviewBoards(GameDate today)
    {
        var facts = new List<BoardFact>();
        var fieldSize = ActiveTeams(today).Count;
        foreach (var snapshot in _book.Section.Boards.ToArray())
        {
            if (snapshot.Principal is not PrincipalRecord principal || snapshot.ExpectedPosition <= 0)
            {
                continue;
            }

            var target = snapshot.ConfidenceTenths;
            if (_book.Facts.Number(snapshot.Organization, ObjectiveFactKeys.ChampionshipPosition) is decimal position)
            {
                var margin = ReputationModel.Margin(snapshot.ExpectedPosition, (int)Math.Round(position, MidpointRounding.AwayFromZero), fieldSize);
                target = ReputationModel.TargetConfidenceTenths(margin);
            }

            var cash = _book.Facts.Number(snapshot.Organization, ObjectiveFactKeys.Cash) is decimal money ? (long?)Math.Round(money) : null;
            var confidence = ReputationModel.ReviewConfidence(snapshot.ConfidenceTenths, target, ReputationModel.CashPenaltyTenths(cash, snapshot.LastCash));
            var threshold = ReputationModel.DismissThresholdTenths(snapshot.Patience);
            var protectedNow = principal.Founder || today <= principal.ProtectedUntil;
            var streak = confidence < threshold && !protectedNow ? snapshot.LowStreak + 1 : 0;
            var needed = ReputationModel.ReviewsToDismiss(snapshot.Patience);
            var reviewed = snapshot with { ConfidenceTenths = confidence, LowStreak = streak, LastReview = today, LastTargetTenths = target, LastCash = cash };
            _book.Update(_book.Section.WithBoard(reviewed));
            if (streak >= needed)
            {
                facts.AddRange(Dismiss(reviewed, today, threshold, needed));
            }
        }

        return facts;
    }

    /// <summary>
    /// Applies the effect of an objective the board granted when it is settled: the board's confidence moves by the stated amount.
    /// The effect is read from the objective, so the player saw it when the objective was granted (DESIGN section 3.2).
    /// </summary>
    public void ApplyObjectiveOutcome(Objective objective, bool met)
    {
        ArgumentNullException.ThrowIfNull(objective);
        if (objective.KindKey is not (BoardKeys.ObjectiveSeason or BoardKeys.ObjectiveMultiYear)
            || _book.Section.Board(objective.Owner) is not BoardRecord board)
        {
            return;
        }

        var effect = met ? objective.EffectOnMet : objective.EffectOnFailed;
        if (!effect.Arguments.TryGetValue("tenths", out var text)
            || !int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var tenths))
        {
            return;
        }

        var updated = board with { ConfidenceTenths = BoardEstimates.Clamp(board.ConfidenceTenths + tenths) };
        _book.Update(_book.Section.WithBoard(updated));
        if (!met
            && effect.Arguments.TryGetValue("dismiss", out var dismiss)
            && dismiss == "1"
            && updated.Principal is { } principal
            && !principal.Founder
            && objective.Deadline > principal.ProtectedUntil)
        {
            Dismiss(updated, objective.Deadline, ReputationModel.DismissThresholdTenths(updated.Patience), ReputationModel.ReviewsToDismiss(updated.Patience));
        }
    }

    /// <summary>Grants the season objective for a chosen ambition. The expected position stays the public one until this runs.</summary>
    public void GrantSeason(OrganizationId organization, int expected, int fieldSize, SeasonAmbition ambition, decimal baseline, GameDate today)
    {
        var target = SeasonTarget.Position(expected, fieldSize, ambition);
        Grant(
            organization,
            BoardKeys.ObjectiveSeason,
            BoardKeys.ReasonSeason,
            target,
            baseline,
            GameDate.SeasonEnd(today.Year),
            SeasonTarget.RewardTenths(ambition),
            today,
            SeasonTarget.PenaltyTenths(ambition),
            SeasonTarget.KeyOf(ambition),
            ambition == SeasonAmbition.Ambitious);
        var board = _book.Section.Board(organization);
        if (board is not null)
        {
            _book.Update(_book.Section.WithBoard(board with { ExpectedPosition = target }));
        }
    }

    /// <summary>Applies the option of a season-target decision. A second answer, or a team that already has a season objective, changes nothing.</summary>
    public void AcceptSeasonTarget(InboxItem item, string optionId, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!SeasonTarget.TryParse(optionId, out var ambition)
            || !item.Arguments.TryGetValue(OrganizationArgument, out var organizationText)
            || !int.TryParse(item.Arguments["expected"], NumberStyles.None, CultureInfo.InvariantCulture, out var expected)
            || !int.TryParse(item.Arguments["field"], NumberStyles.None, CultureInfo.InvariantCulture, out var fieldSize))
        {
            return;
        }

        var found = _book.World.Organizations.FirstOrDefault(candidate => candidate.Id.Value == organizationText)?.Id;
        if (found is not OrganizationId owner)
        {
            return;
        }

        if (_book.Objectives.OwnedBy(owner).Any(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason))
        {
            return;
        }

        var baseline = _book.Facts.Number(owner, ObjectiveFactKeys.ChampionshipPosition) ?? expected;
        GrantSeason(owner, expected, fieldSize, ambition, baseline, today);
    }

    // ---------------------------------------------------------------- dismissal and replacement

    private IReadOnlyList<BoardFact> Dismiss(BoardRecord board, GameDate today, int threshold, int needed)
    {
        var principal = board.Principal ?? throw new InvalidOperationException("A board with no principal cannot dismiss.");
        var facts = new List<BoardFact>();
        TraceDismissal(board, principal, today, threshold, needed);
        if (principal.Kind == PrincipalKind.Human)
        {
            var manager = new ManagerId(principal.Subject);
            var severance = Severance(today);
            facts.Add(Leave(manager, board.Organization, today, severance, BoardKeys.ReputationDismissed, BoardEstimates.DismissalTenths, BoardKeys.NoticeDismissed, BoardEventTypes.ManagerDismissed));
            facts.AddRange(HireAiPrincipal(board.Organization, today, null, BoardKeys.NoticeDismissed));
            return facts;
        }

        var person = PersonOf(principal.Subject);
        EndPrincipalContracts(person, board.Organization, today);
        _book.Update(_book.Section
            .WithReputationChange(principal.Subject, today, -BoardEstimates.DismissalTenths, BoardKeys.ReputationDismissed)
            .WithBoard(board with { Principal = null }));
        facts.Add(new BoardFact(
            BoardEventTypes.ManagerDismissed,
            new BoardFactPayload(board.Organization.Value, principal.Subject, PrincipalKind.Ai.ToString(), string.Empty, string.Empty, BoardKeys.NoticeDismissed, 0)));
        facts.AddRange(HireAiPrincipal(board.Organization, today, person, BoardKeys.NoticeDismissed));
        return facts;
    }

    /// <summary>A human principal leaves the team (dismissed or resigned): their open work ends, they are unemployed, the seat is empty.</summary>
    private BoardFact Leave(ManagerId manager, OrganizationId organization, GameDate today, long severance, string reputationReason, int reputationTenths, string noticeKey, string eventType)
    {
        CloseOpenWork(manager, today);
        var section = _book.Section
            .WithReputationChange(manager.Value, today, -reputationTenths, reputationReason)
            .WithUnemployed(new UnemployedRecord(manager.Value, today, organization, severance, 0, null));
        var board = section.Board(organization) ?? throw new InvalidOperationException("The team has no board.");
        _book.Update(section.WithBoard(board with { Principal = null }));
        if (severance > 0)
        {
            _book.Severance.Pay(organization, manager.Value, severance, today);
        }

        if (_managers.Contains(manager))
        {
            var name = _book.World.GetOrganization(organization).NameOn(today);
            _inbox.Post(
                _managers,
                manager,
                new InboxItemDraft(
                    NoticeKind,
                    noticeKey,
                    [new("organization", name), new("severance", severance.ToString(CultureInfo.InvariantCulture))],
                    null,
                    null,
                    null),
                today);
        }

        return new BoardFact(
            eventType,
            new BoardFactPayload(organization.Value, manager.Value, PrincipalKind.Human.ToString(), string.Empty, string.Empty, noticeKey, severance));
    }

    /// <summary>
    /// Hires a principal for the team from the people who are free to take it, by a draw from the Market stream, and rerolls the
    /// archetype to a different one so the team visibly changes direction. With nobody free the seat stays empty and is tried again
    /// on the next day. The new principal is protected like any new one.
    /// </summary>
    private IReadOnlyList<BoardFact> HireAiPrincipal(OrganizationId organization, GameDate today, PersonId? outgoing, string reasonKey)
    {
        var board = _book.Section.Board(organization) ?? throw new InvalidOperationException("The team has no board.");
        var end = GameDate.SeasonEnd(today.Year + 1);
        var candidates = _book.World.Persons
            .Where(person => !person.IsRetired
                && (outgoing is null || person.Id != outgoing.Value)
                && person.Roles.Any(role => role.IsStaff && role.StaffRole == StaffRole.TeamPrincipal)
                && _book.World.FindExclusiveOverlap(person.Id, today, end) is null)
            .OrderBy(person => person.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        var rng = Draw("appoint", organization, today);
        var chosen = candidates[rng.NextInt(0, candidates.Length)];
        var others = Archetypes.Where(archetype => archetype != board.Archetype).ToArray();
        var archetype = others[rng.NextInt(0, others.Length)];
        var (world, _) = _book.World.AddContract(new ContractSpec(
            chosen.Id, organization, ContractRole.Staff(StaffRole.TeamPrincipal), today, end, 0, true, null, null));
        _book.Contracts.Update(world);
        var section = _book.Section.WithInitialReputation(chosen.Id.Value, BoardEstimates.InitialReputationTenths);
        var reputation = section.ReputationTenths(chosen.Id.Value);
        _book.Update(section.WithBoard(board with
        {
            Principal = new PrincipalRecord(PrincipalKind.Ai, chosen.Id.Value, today, ReputationModel.ProtectedUntil(today, reputation), false),
            Archetype = archetype,
            ConfidenceTenths = BoardEstimates.InitialConfidenceTenths,
            LowStreak = 0,
            LastReview = null,
            LastTargetTenths = null,
        }));
        TraceAppointment(organization, today, candidates.Select(person => person.Id.Value).ToArray(), chosen.Id.Value, board.Archetype, archetype);
        return
        [
            new BoardFact(
                BoardEventTypes.ManagerAppointed,
                new BoardFactPayload(organization.Value, chosen.Id.Value, PrincipalKind.Ai.ToString(), outgoing?.Value ?? string.Empty, archetype, reasonKey, 0)),
        ];
    }

    private void EndPrincipalContracts(PersonId person, OrganizationId organization, GameDate today)
    {
        foreach (var contract in _book.World.Contracts
            .Where(candidate => candidate.PersonId == person
                && candidate.OrganizationId == organization
                && candidate.Role.IsStaff
                && candidate.Role.StaffRole == StaffRole.TeamPrincipal
                && candidate.End >= today)
            .ToArray())
        {
            _book.Contracts.Update(contract.Start >= today
                ? _book.World.RemoveContract(contract.Id)
                : _book.World.WithContractTerm(contract.Id, today.AddDays(-1), null));
        }
    }

    /// <summary>Ends what a manager who leaves can no longer carry on: their open negotiations and the decisions waiting for them.</summary>
    private void CloseOpenWork(ManagerId manager, GameDate today)
    {
        var contracts = new ContractEngine(_book.Contracts, _inbox, _managers);
        foreach (var negotiation in _book.Contracts.Section.Active().Where(candidate => candidate.ManagerId == manager.Value).ToArray())
        {
            contracts.WalkAway(manager, negotiation.Id, today);
        }

        foreach (var item in _inbox.Section.ItemsOf(manager.Value).Where(candidate => candidate.IsOpen).ToArray())
        {
            _inbox.Withdraw(_managers, item.Id, today);
        }
    }

    // ---------------------------------------------------------------- reputation at the end of a season

    private void SettleSeasonReputation(GameDate today)
    {
        var fieldSize = ActiveTeams(today).Count;
        foreach (var board in _book.Section.Boards.ToArray())
        {
            if (board.Principal is not PrincipalRecord principal
                || board.ExpectedPosition <= 0
                || _book.Facts.Number(board.Organization, ObjectiveFactKeys.ChampionshipPosition) is not decimal position)
            {
                continue;
            }

            var cashBelowZero = _book.Facts.Number(board.Organization, ObjectiveFactKeys.Cash) is decimal cash && cash < 0;
            var delta = ReputationModel.SeasonReputationTenths(
                board.ExpectedPosition,
                (int)Math.Round(position, MidpointRounding.AwayFromZero),
                fieldSize,
                cashBelowZero,
                _book.Facts.Number(board.Organization, BoardFactKeys.PeopleDevelopment));
            _book.Update(_book.Section.WithReputationChange(principal.Subject, today, delta, BoardKeys.ReputationSeason));
        }
    }

    // ---------------------------------------------------------------- the job search

    /// <summary>
    /// For every manager without a team: an ordinary offer from a team that would take a principal, picked by a draw among those
    /// that ask no more reputation than the manager has; and, whatever the reputation, a minimum-quality offer once
    /// <see cref="BoardEstimates.GuaranteeWindowDays"/> have passed with none, so a dismissed player is never stuck.
    /// </summary>
    private IReadOnlyList<BoardFact> OfferJobs(GameDate today)
    {
        var facts = new List<BoardFact>();
        foreach (var record in _book.Section.Unemployed.ToArray())
        {
            var open = _inbox.Section.ItemsOf(record.Manager).Where(item => item.IsOpen && item.Kind == OfferKind).ToArray();
            var reference = record.LastOfferOn ?? record.Since;
            var waited = reference.DaysUntil(today);
            var delay = record.LastOfferOn is null ? BoardEstimates.FirstOfferDelayDays : BoardEstimates.OfferIntervalDays;
            var ordinary = waited >= delay && open.Length < BoardEstimates.MaxOpenOffers;
            var guarantee = open.Length == 0 && waited >= BoardEstimates.GuaranteeWindowDays;
            if (!ordinary && !guarantee)
            {
                continue;
            }

            var offered = open.Select(item => item.Arguments[OrganizationArgument]).ToHashSet(StringComparer.Ordinal);
            var hiring = _book.Section.Boards
                .Where(board => board.Principal is { Kind: PrincipalKind.Ai }
                    && board.Organization != record.FormerOrganization
                    && !offered.Contains(board.Organization.Value))
                .ToArray();
            var reputation = _book.Section.ReputationTenths(record.Manager);
            BoardRecord? pick = null;
            if (ordinary)
            {
                var eligible = hiring
                    .Where(board => board.ConfidenceTenths < BoardEstimates.NeedsPrincipalBelowTenths
                        && today > board.Principal!.ProtectedUntil
                        && Required(board.Organization, today) <= reputation)
                    .ToArray();
                if (eligible.Length > 0)
                {
                    pick = eligible[Draw("offer:" + record.Manager, today).NextInt(0, eligible.Length)];
                }
            }

            if (pick is null && guarantee)
            {
                pick = hiring
                    .OrderBy(board => Required(board.Organization, today))
                    .ThenBy(board => board.Organization.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
            }

            if (pick is not null)
            {
                facts.Add(PostOffer(record, pick.Organization, today));
            }
        }

        return facts;
    }

    private BoardFact PostOffer(UnemployedRecord record, OrganizationId organization, GameDate today)
    {
        var manager = new ManagerId(record.Manager);
        var name = _book.World.GetOrganization(organization).NameOn(today);
        _inbox.Post(
            _managers,
            manager,
            new InboxItemDraft(
                OfferKind,
                BoardKeys.OfferSubject,
                [
                    new(OrganizationArgument, organization.Value),
                    new("name", name),
                    new("required", (Required(organization, today) / 10).ToString(CultureInfo.InvariantCulture)),
                ],
                [
                    new InboxOption(OptionAccept, BoardKeys.OfferAcceptLabel, BoardKeys.OfferAcceptConsequence),
                    new InboxOption(OptionDecline, BoardKeys.OfferDeclineLabel, BoardKeys.OfferDeclineConsequence),
                ],
                today.AddDays(BoardEstimates.OfferValidDays),
                OptionDecline),
            today);
        _book.Update(_book.Section.WithUnemployed(record with { OffersMade = record.OffersMade + 1, LastOfferOn = today }));
        return new BoardFact(
            BoardEventTypes.JobOffered,
            new BoardFactPayload(organization.Value, record.Manager, PrincipalKind.Human.ToString(), string.Empty, string.Empty, BoardKeys.OfferSubject, 0));
    }

    // ---------------------------------------------------------------- facts the board reads

    /// <summary>The organizations that run a team today, in ordinal order of their id.</summary>
    public IReadOnlyList<Organization> ActiveTeams(GameDate today) => PublicStrength.ActiveTeams(_book.World, today);

    /// <summary>The rank of the organization's budget among the active teams (1 is the richest). A public fact.</summary>
    public int BudgetRank(OrganizationId organization, GameDate today) =>
        PublicStrength.BudgetRank(_book.World, organization, today);

    /// <summary>The reputation in tenths that a team asks of a new principal, from its prestige.</summary>
    public int Required(OrganizationId organization, GameDate today) =>
        ReputationModel.RequiredReputationTenths(_book.Contracts.Environment.Appeal.Appeal(organization, today).Prestige);

    /// <summary>The organization an offer item names, or null when it names none that exists.</summary>
    public OrganizationId? OfferedOrganization(InboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != OfferKind || !item.Arguments.TryGetValue(OrganizationArgument, out var text))
        {
            return null;
        }

        foreach (var organization in _book.World.Organizations)
        {
            if (organization.Id.Value == text)
            {
                return organization.Id;
            }
        }

        return null;
    }

    private PersonId PersonOf(string subject) =>
        _book.World.Persons.FirstOrDefault(person => person.Id.Value == subject)?.Id
        ?? throw new InvalidOperationException($"The board names '{subject}' as a principal, but the world has no such person.");

    private long Severance(GameDate today)
    {
        var reference = _book.Contracts.Environment.Pay.Reference(today.Season, NegotiationSubject.Staff(StaffRole.TeamPrincipal), BoardEstimates.SeveranceStars);
        return Math.Max(1L, (long)Math.Round(reference * BoardEstimates.SeveranceShare, MidpointRounding.AwayFromZero));
    }

    private void Grant(
        OrganizationId organization,
        string kind,
        string reason,
        int target,
        decimal baseline,
        GameDate deadline,
        int tenths,
        GameDate today,
        int? penaltyTenths = null,
        string? ambition = null,
        bool dismissOnFail = false)
    {
        var penalty = penaltyTenths ?? tenths;
        var metPoints = (tenths / 10).ToString(CultureInfo.InvariantCulture);
        var failPoints = (penalty / 10).ToString(CultureInfo.InvariantCulture);
        var met = new List<KeyValuePair<string, string>>
        {
            new("points", metPoints),
            new("tenths", tenths.ToString(CultureInfo.InvariantCulture)),
        };
        var failed = new List<KeyValuePair<string, string>>
        {
            new("points", failPoints),
            new("tenths", (-penalty).ToString(CultureInfo.InvariantCulture)),
        };
        if (ambition is not null)
        {
            met.Add(new("ambition", ambition));
            failed.Add(new("ambition", ambition));
            failed.Add(new("dismiss", dismissOnFail ? "1" : "0"));
        }

        var (section, _) = _book.Objectives.Add(
            new ObjectiveDraft(
                organization,
                organization,
                kind,
                reason,
                new ChampionshipPositionAtMost(target),
                baseline,
                deadline,
                new ObjectiveEffect(BoardKeys.EffectConfidenceUp, met),
                new ObjectiveEffect(BoardKeys.EffectConfidenceDown, failed)),
            today);
        _book.Update(section);
    }

    private void PostSeasonTarget(string managerId, OrganizationId organization, int expected, int fieldSize, GameDate today)
    {
        var manager = new ManagerId(managerId);
        if (!_managers.Contains(manager))
        {
            GrantSeason(organization, expected, fieldSize, SeasonAmbition.Expected, expected, today);
            return;
        }

        string Text(SeasonAmbition ambition) =>
            SeasonTarget.Position(expected, fieldSize, ambition).ToString(CultureInfo.InvariantCulture);
        string Points(SeasonAmbition ambition) =>
            (SeasonTarget.RewardTenths(ambition) / 10).ToString(CultureInfo.InvariantCulture);
        string Loss(SeasonAmbition ambition) =>
            (SeasonTarget.PenaltyTenths(ambition) / 10).ToString(CultureInfo.InvariantCulture);
        _inbox.Post(
            _managers,
            manager,
            new InboxItemDraft(
                SeasonTargetKind,
                BoardKeys.SeasonTargetSubject,
                [
                    new(OrganizationArgument, organization.Value),
                    new("expected", expected.ToString(CultureInfo.InvariantCulture)),
                    new("field", fieldSize.ToString(CultureInfo.InvariantCulture)),
                    new("safeTarget", Text(SeasonAmbition.Safe)),
                    new("expectedTarget", Text(SeasonAmbition.Expected)),
                    new("ambitiousTarget", Text(SeasonAmbition.Ambitious)),
                    new("safePoints", Points(SeasonAmbition.Safe)),
                    new("expectedPoints", Points(SeasonAmbition.Expected)),
                    new("ambitiousPoints", Points(SeasonAmbition.Ambitious)),
                    new("safeLoss", Loss(SeasonAmbition.Safe)),
                    new("expectedLoss", Loss(SeasonAmbition.Expected)),
                    new("ambitiousLoss", Loss(SeasonAmbition.Ambitious)),
                ],
                [
                    new InboxOption(SeasonTarget.Safe, BoardKeys.SeasonTargetSafeLabel, BoardKeys.SeasonTargetSafeConsequence),
                    new InboxOption(SeasonTarget.Expected, BoardKeys.SeasonTargetExpectedLabel, BoardKeys.SeasonTargetExpectedConsequence),
                    new InboxOption(SeasonTarget.Ambitious, BoardKeys.SeasonTargetAmbitiousLabel, BoardKeys.SeasonTargetAmbitiousConsequence),
                ],
                today.AddDays(BoardEstimates.SeasonTargetDecisionDays),
                SeasonTarget.Expected),
            today);
    }

    private bool TargetPending(string managerId, OrganizationId organization) =>
        _inbox.Section.ItemsOf(managerId).Any(item =>
            item.IsOpen
            && item.Kind == SeasonTargetKind
            && item.Arguments.TryGetValue(OrganizationArgument, out var id)
            && id == organization.Value);

    private string? StyleOf(OrganizationId organization) =>
        _book.World.Section<PrincipalsSection>(PrincipalsSection.SectionName)?.Of(organization)?.Archetype;

    private void TraceSeasonTarget(BoardRecord board, SeasonAmbition chosen, GameDate today)
    {
        var trace = _book.Contracts.Environment.Trace;
        if (!trace.IsEnabled || board.Principal is not { } principal)
        {
            return;
        }

        var style = StyleOf(board.Organization) ?? "-";
        TraceOption Option(SeasonAmbition ambition)
        {
            var picked = ambition == chosen;
            return new TraceOption(
                SeasonTarget.KeyOf(ambition),
                picked ? 1 : 0,
                [new TraceFactor("archetype", picked ? 1 : 0, false)],
                false);
        }

        trace.Record(new DecisionTrace(
            new WeekendKey(today.Season, 0),
            principal.Subject,
            0,
            "board.seasonTarget:" + board.Organization.Value,
            [Option(SeasonAmbition.Safe), Option(SeasonAmbition.Expected), Option(SeasonAmbition.Ambitious)],
            SeasonTarget.KeyOf(chosen),
            "archetype " + style + " picks " + SeasonTarget.KeyOf(chosen),
            null,
            true,
            new Dictionary<string, string> { ["archetype"] = style }));
    }

    /// <summary>A draw keyed by purpose, organization and date, from the Market stream of the season (INV-004).</summary>
    private Xoshiro256StarStar Draw(string purpose, GameDate today) =>
        RngStream.Derive(_book.MasterSeed, RngStreamName.Market, today.Year).DeriveChild("board:" + purpose + ":" + today);

    private Xoshiro256StarStar Draw(string purpose, OrganizationId organization, GameDate today) =>
        Draw(purpose + ":" + organization.Value, today);

    // ---------------------------------------------------------------- traces (passive, INV-006)

    private void TraceDismissal(BoardRecord board, PrincipalRecord principal, GameDate today, int threshold, int needed)
    {
        var trace = _book.Contracts.Environment.Trace;
        if (!trace.IsEnabled)
        {
            return;
        }

        var gap = (threshold - board.ConfidenceTenths) / 10.0;
        trace.Record(new DecisionTrace(
            new WeekendKey(today.Season, 0),
            "board:" + board.Organization.Value,
            TraceLevel,
            "board.dismiss:" + board.Organization.Value,
            [
                new TraceOption("dismiss", gap, [new TraceFactor("confidenceBelowBar", gap, false)], true),
                new TraceOption("keep", -gap, [], false),
            ],
            "dismiss",
            "confidence " + Tenths(board.ConfidenceTenths) + " below the bar of " + Tenths(threshold) + " for " + board.LowStreak.ToString(CultureInfo.InvariantCulture)
                + " reviews in a row (needed " + needed.ToString(CultureInfo.InvariantCulture) + ")",
            null,
            true,
            new Dictionary<string, string>
            {
                ["principal"] = principal.Subject,
                ["kind"] = principal.Kind.ToString(),
                ["patience"] = board.Patience.ToString(CultureInfo.InvariantCulture),
                ["expected"] = board.ExpectedPosition.ToString(CultureInfo.InvariantCulture),
            }));
    }

    private void TraceAppointment(OrganizationId organization, GameDate today, string[] candidates, string chosen, string oldArchetype, string newArchetype)
    {
        var trace = _book.Contracts.Environment.Trace;
        if (!trace.IsEnabled)
        {
            return;
        }

        trace.Record(new DecisionTrace(
            new WeekendKey(today.Season, 0),
            "board:" + organization.Value,
            TraceLevel,
            "board.appoint:" + organization.Value,
            candidates.Select(candidate => new TraceOption(candidate, 0.0, [], candidate == chosen)).ToArray(),
            chosen,
            "drawn from " + candidates.Length.ToString(CultureInfo.InvariantCulture) + " free principals; archetype " + oldArchetype + " rerolled to " + newArchetype,
            null,
            true,
            new Dictionary<string, string> { ["oldArchetype"] = oldArchetype, ["newArchetype"] = newArchetype }));
    }

    private static string Tenths(int tenths) => (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
}
