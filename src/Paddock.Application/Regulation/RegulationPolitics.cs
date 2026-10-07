using System.Globalization;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;

namespace Paddock.Application.Regulation;

/// <summary>What a team would pay and when it may propose, as the player reads it before filing.</summary>
/// <param name="FeeCents">The fee in cents: a share of the revenue of the last completed season, never below the floor.</param>
/// <param name="BasisCents">The revenue the share is taken of.</param>
/// <param name="ProposeFromSeason">The first season the team may propose.</param>
/// <param name="InCooldown">True when <paramref name="ProposeFromSeason"/> is after the season asked about.</param>
public sealed record ProposalQuote(long FeeCents, long BasisCents, int ProposeFromSeason, bool InCooldown);

/// <summary>One change a vote could bring in a series: the dimension, what it is now, the value it would move to and how they differ.</summary>
public sealed record RuleChoice(string DimensionId, string Current, string Value, ValueTraits Delta, CalendarPolicy.Option? Calendar);

/// <summary>
/// The political life of the series (#275): who may propose, what a proposal costs, how proposals become one ballot item per
/// dimension, how the FIA brings its own votes, how a ballot item is voted and counted, and what is stored for the next season. The
/// player's commands and the AI teams use the same methods and the same rules. Nothing here draws from a shared stream: every
/// random number is a child of the Regulations stream of the season, keyed by what it is for, so a change in one place never moves
/// another (INV-004).
/// </summary>
public sealed class RegulationPolitics
{
    private readonly RegulationBook _book;
    private readonly RegulationEnvironment _environment;
    private readonly InboxBook? _inbox;

    // The FIA's calendar for a series and a season is a pure function of the seed, so it is worked out once, not on every day.
    private readonly Dictionary<(string SeriesId, int Season), IReadOnlyList<GameDate>> _fiaDays = [];

    public RegulationPolitics(RegulationBook book, RegulationEnvironment environment, InboxBook? inbox = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        _inbox = inbox;
    }

    public RegulationEnvironment Environment => _environment;

    // ---------------------------------------------------------------- opening

    /// <summary>
    /// The section of a career that opens with voted rules: every series with the authored rules of the opening season, every team
    /// with a leaning and a starting cooldown. Pure of the world's other sections; the host stores it.
    /// </summary>
    public RegulationsSection Open(WorldState world, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        var series = new List<SeriesRegulations>();
        foreach (var seriesId in _environment.Series.SeriesIds)
        {
            var opening = RegulationsSection.OpeningSeries(seriesId, today.Year, _environment.AuthoredRules(today.Year).Values);
            series.Add(EnsureTeams(opening, _environment.Series.TeamsOf(seriesId, world, today), today.Year, opening: true));
        }

        return RegulationsSection.Create(series);
    }

    /// <summary>
    /// Gives every team of the series that has none a political life: a leaning drawn from the Regulations stream and a cooldown. At
    /// the opening of a career the AI teams are staggered, so they do not all propose in the first season: they are ranked by a seeded
    /// hash and take the offsets 0, 1, 2, 0, 1, 2 and so on, which makes them differ and keeps them the same for the same seed. A team a
    /// human runs starts with no cooldown (owner decision 6). A team that appears later draws its offset from its own child stream.
    /// </summary>
    public SeriesRegulations EnsureTeams(SeriesRegulations series, IReadOnlyList<Organization> teams, int season, bool opening)
    {
        var missing = teams.Where(team => series.TeamOf(team.Id.Value) is null).ToList();
        if (missing.Count == 0)
        {
            return series;
        }

        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var span = RegulationEstimates.StartingCooldownMaxSeasons + 1;
        var ai = missing.Where(team => !(opening && _environment.HumanTeams.Contains(team.Id)) && !_environment.IsHuman(team.Id)).ToList();
        if (opening)
        {
            var ranked = ai
                .Select(team => (Team: team.Id.Value, Hash: RegulationSchedule.Child(_environment.MasterSeed, season, "stagger:" + series.SeriesId + ":" + team.Id.Value).NextULong()))
                .OrderBy(entry => entry.Hash)
                .ThenBy(entry => entry.Team, StringComparer.Ordinal)
                .ToList();
            for (var rank = 0; rank < ranked.Count; rank++)
            {
                offsets[ranked[rank].Team] = rank % span;
            }
        }
        else
        {
            foreach (var team in ai)
            {
                offsets[team.Id.Value] = RegulationSchedule.Child(_environment.MasterSeed, season, "stagger:" + series.SeriesId + ":" + team.Id.Value).NextInt(0, span);
            }
        }

        foreach (var team in missing)
        {
            var leaning = (PoliticalLeaning)RegulationSchedule.Child(_environment.MasterSeed, season, "leaning:" + series.SeriesId + ":" + team.Id.Value).NextInt(0, 4);
            var offset = offsets.TryGetValue(team.Id.Value, out var found) ? found : 0;
            series = series.WithTeam(new TeamPolitics(team.Id.Value, leaning, season + offset, 0));
        }

        return series;
    }

    /// <summary>A human sits at the team now. The seats the run was opened with only matter while the career opens (<see cref="EnsureTeams"/>).</summary>
    private bool IsHumanSeat(OrganizationId team) => _environment.IsHuman(team);

    // ---------------------------------------------------------------- reading

    /// <summary>The first season a team may propose and what the proposal would cost it in <paramref name="season"/>.</summary>
    public ProposalQuote? QuoteFor(string seriesId, string teamId, int season)
    {
        if (_book.Section?.Find(seriesId) is not { } series || series.TeamOf(teamId) is not { } politics
            || !CarCommandSupport.TryOrganization(teamId, out var organization))
        {
            return null;
        }

        var (fee, basis) = FeeFor(organization, season);
        return new ProposalQuote(fee, basis, politics.ProposeFromSeason, politics.ProposeFromSeason > season);
    }

    private (long Fee, long Basis) FeeFor(OrganizationId team, int season)
    {
        var finance = _book.Finance;
        var basis = finance.HasBook(team) ? ProposalFee.RevenueBasis(finance.EntriesOf(team), season) : 0;
        return (ProposalFee.Quote(basis, finance.TypicalCents), basis);
    }

    /// <summary>
    /// Every change a vote could bring for the season after <paramref name="voteSeason"/>, minus the dimensions in
    /// <paramref name="excluded"/>: the live rules and the calendar, each value that differs from the one decided so far, makes a
    /// difference, and was not rejected within the memory (unless <paramref name="ignoreMemory"/>, which the FIA uses when the memory has
    /// left it nothing to bring: a small catalogue must not stop it from bringing its four to six votes a year).
    /// </summary>
    public IReadOnlyList<RuleChoice> ChoicesFor(SeriesRegulations series, int voteSeason, ISet<string> excluded, bool ignoreMemory = false)
    {
        var choices = new List<RuleChoice>();
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (dimension, value) in series.NextValues ?? series.Values)
        {
            rules[dimension] = value;
        }

        foreach (var rule in LiveRules.All)
        {
            if (excluded.Contains(rule.DimensionId)
                || !rules.TryGetValue(rule.DimensionId, out var current)
                || !_environment.Specs.TryGetValue(rule.DimensionId, out var spec))
            {
                continue;
            }

            foreach (var value in rule.Values)
            {
                if (value == current || !Allowed(spec, value) || !LiveRules.HasEffect(rule.DimensionId, current, value)
                    || (!ignoreMemory && RejectedUntil(series, rule.DimensionId, value, voteSeason) is not null))
                {
                    continue;
                }

                choices.Add(new RuleChoice(rule.DimensionId, current, value, LiveRules.TraitsOf(rule.DimensionId, value) - LiveRules.TraitsOf(rule.DimensionId, current), null));
            }
        }

        if (_environment.Layouts.Count > 0 && _environment.Assignments.Count > 0)
        {
            var policy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (dimension, value) in series.NextCalendar ?? series.Calendar)
            {
                policy[dimension] = value;
            }

            foreach (var option in CalendarPolicy.Options(voteSeason + 1, _environment.Layouts, _environment.Assignments, policy))
            {
                if (excluded.Contains(option.DimensionId))
                {
                    continue;
                }

                foreach (var alternative in option.Alternatives)
                {
                    if ((!ignoreMemory && RejectedUntil(series, option.DimensionId, alternative, voteSeason) is not null)
                        || !CalendarPolicy.KeepsEnoughRounds(voteSeason + 1, _environment.Layouts, _environment.Assignments, policy, option.DimensionId, alternative))
                    {
                        continue;
                    }

                    choices.Add(new RuleChoice(
                        option.DimensionId,
                        option.Current,
                        alternative,
                        CalendarPolicy.TraitsOf(alternative) - CalendarPolicy.TraitsOf(option.Current),
                        option));
                }
            }
        }

        choices.Sort(static (left, right) =>
        {
            var byDimension = string.CompareOrdinal(left.DimensionId, right.DimensionId);
            return byDimension != 0 ? byDimension : string.CompareOrdinal(left.Value, right.Value);
        });
        return choices;
    }

    private static bool Allowed(RuleDimensionSpec spec, string value) =>
        spec.Kind == RuleDimensionKind.Choice ? spec.Values.Contains(value) : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number >= spec.Min && number <= spec.Max;

    /// <summary>The first season a (dimension, value) rejected in the memory may be proposed again, or null when it is not in the memory.</summary>
    private static int? RejectedUntil(SeriesRegulations series, string dimensionId, string value, int voteSeason)
    {
        var start = voteSeason + 1;
        foreach (var rejected in series.Rejected)
        {
            if (rejected.DimensionId == dimensionId && rejected.Value == value && start - rejected.Season <= RegulationEstimates.RejectionMemorySeasons)
            {
                return rejected.Season + RegulationEstimates.RejectionMemorySeasons + 1;
            }
        }

        return null;
    }

    private RuleOption OptionFor(RuleChoice choice, string teamId) => new(
        choice.DimensionId,
        choice.Current,
        choice.Value,
        choice.Delta,
        choice.Calendar is { } option ? CalendarPolicy.HomeEffect(option, choice.Value, _environment.CountryOf(teamId)) : 0);

    // ---------------------------------------------------------------- proposing

    /// <summary>
    /// The reason a team cannot file this proposal today, or null. A refusal for the cooldown says in which season the team may propose
    /// again. The fee never blocks: a team may pay it with cash that goes below zero (owner decision 8).
    /// </summary>
    public TranslationMessage? CheckPropose(string seriesId, string teamId, string dimensionId, string value, GameDate today)
    {
        if (!_environment.Votes)
        {
            return TranslationMessage.Of(RegulationKeys.NotVoted);
        }

        if (_book.Section?.Find(seriesId) is not { } series)
        {
            return TranslationMessage.Of(RegulationKeys.UnknownSeries);
        }

        if (series.TeamOf(teamId) is not { } politics || !CarCommandSupport.TryOrganization(teamId, out var organization)
            || !_environment.Series.TeamsOf(seriesId, _book.World, today).Any(team => team.Id == organization))
        {
            return TranslationMessage.Of(RegulationKeys.UnknownTeam);
        }

        if (today < RegulationSchedule.ProposalsOpen(today.Year) || today > RegulationSchedule.ProposalsClose(today.Year))
        {
            var reopens = today < RegulationSchedule.ProposalsOpen(today.Year)
                ? RegulationSchedule.ProposalsOpen(today.Year)
                : RegulationSchedule.ProposalsOpen(today.Year + 1);
            return TranslationMessage.Of(RegulationKeys.WindowClosed, ("opens", reopens.ToString()));
        }

        if (today.Year < politics.ProposeFromSeason)
        {
            return TranslationMessage.Of(RegulationKeys.Cooldown, ("season", politics.ProposeFromSeason.ToString(CultureInfo.InvariantCulture)));
        }

        if (series.Pending.Any(pending => pending.TeamId == teamId))
        {
            return TranslationMessage.Of(RegulationKeys.AlreadyProposed);
        }

        var onBallot = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in series.Ballot)
        {
            if (item.Season == today.Year)
            {
                onBallot.Add(item.DimensionId);
            }
        }

        var choices = ChoicesFor(series, today.Year, onBallot);
        if (choices.Any(choice => choice.DimensionId == dimensionId && choice.Value == value))
        {
            return _book.Finance.HasBook(organization) ? null : TranslationMessage.Of(RegulationKeys.NoBooks);
        }

        return Explain(series, today.Year, dimensionId, value);
    }

    /// <summary>Why <c>dimension = value</c> is not among the choices: the first reason that applies.</summary>
    private TranslationMessage Explain(SeriesRegulations series, int voteSeason, string dimensionId, string value)
    {
        var current = (series.NextValues ?? series.Values).FirstOrDefault(pair => pair.Key == dimensionId).Value;
        if (CalendarPolicy.IsCalendarDimension(dimensionId))
        {
            var policy = (series.NextCalendar ?? series.Calendar).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var option = CalendarPolicy.Options(voteSeason + 1, _environment.Layouts, _environment.Assignments, policy).FirstOrDefault(o => o.DimensionId == dimensionId);
            if (option is null)
            {
                return TranslationMessage.Of(RegulationKeys.NotLive);
            }

            if (!CalendarPolicy.IsKnownValue(value) || (!option.Alternatives.Contains(value) && value != option.Current))
            {
                return TranslationMessage.Of(RegulationKeys.ValueNotAllowed);
            }

            if (value == option.Current)
            {
                return TranslationMessage.Of(RegulationKeys.SameValue);
            }

            if (RejectedUntil(series, dimensionId, value, voteSeason) is { } calendarBack)
            {
                return TranslationMessage.Of(RegulationKeys.RecentlyRejected, ("season", calendarBack.ToString(CultureInfo.InvariantCulture)));
            }

            return TranslationMessage.Of(RegulationKeys.TooFewRounds, ("rounds", RegulationEstimates.MinimumRounds.ToString(CultureInfo.InvariantCulture)));
        }

        if (LiveRules.Find(dimensionId) is not { } rule || current is null)
        {
            return TranslationMessage.Of(RegulationKeys.NotLive);
        }

        if (!rule.Values.Contains(value))
        {
            return TranslationMessage.Of(RegulationKeys.ValueNotAllowed);
        }

        if (value == current)
        {
            return TranslationMessage.Of(RegulationKeys.SameValue);
        }

        if (!LiveRules.HasEffect(dimensionId, current, value))
        {
            return TranslationMessage.Of(RegulationKeys.NoEffect);
        }

        if (RejectedUntil(series, dimensionId, value, voteSeason) is { } back)
        {
            return TranslationMessage.Of(RegulationKeys.RecentlyRejected, ("season", back.ToString(CultureInfo.InvariantCulture)));
        }

        // The dimension is already on this season's ballot.
        return TranslationMessage.Of(RegulationKeys.AlreadyProposed);
    }

    /// <summary>
    /// Files a proposal: the fee goes to the ledger with its reason and is not refunded, the team's cooldown starts, and the proposal
    /// waits for the team ballot. The caller has checked it with <see cref="CheckPropose"/>. Returns the fee.
    /// </summary>
    public long Propose(string seriesId, string teamId, string dimensionId, string value, GameDate today)
    {
        var reason = CheckPropose(seriesId, teamId, dimensionId, value, today);
        if (reason is not null)
        {
            throw new InvalidOperationException("The proposal was refused: " + reason.Key + ".");
        }

        var section = _book.Section ?? throw new InvalidOperationException("The career has no regulations section.");
        var series = section.Find(seriesId)!;
        var finance = _book.Finance;
        series = Propose(series, ref finance, teamId, dimensionId, value, today, out var fee);
        _book.Write(section.WithSeries(series), finance);
        return fee;
    }

    private SeriesRegulations Propose(SeriesRegulations series, ref FinanceSection finance, string teamId, string dimensionId, string value, GameDate today, out long fee)
    {
        CarCommandSupport.TryOrganization(teamId, out var organization);
        var basis = finance.HasBook(organization) ? ProposalFee.RevenueBasis(finance.EntriesOf(organization), today.Year) : 0;
        fee = ProposalFee.Quote(basis, finance.TypicalCents);
        finance = finance.Post(organization, today, LedgerCategories.Other, series.SeriesId, -fee, RegulationKeys.LedgerProposalFee);
        var politics = series.TeamOf(teamId)!;
        series = series.WithTeam(politics.WithProposeFrom(today.Year + RegulationEstimates.CooldownSeasons + 1));
        return series.WithPending([.. series.Pending, new PendingProposal(teamId, dimensionId, value, today, fee)]);
    }

    // ---------------------------------------------------------------- voting

    /// <summary>The reason a team's vote cannot be cast on this item today, or null.</summary>
    public TranslationMessage? CheckCast(string seriesId, string teamId, string itemId, string option, int spent, GameDate today)
    {
        if (!_environment.Votes)
        {
            return TranslationMessage.Of(RegulationKeys.NotVoted);
        }

        if (_book.Section?.Find(seriesId) is not { } series)
        {
            return TranslationMessage.Of(RegulationKeys.UnknownSeries);
        }

        if (series.TeamOf(teamId) is not { } politics
            || !CarCommandSupport.TryOrganization(teamId, out var organization)
            || !_environment.Series.TeamsOf(seriesId, _book.World, today).Any(team => team.Id == organization))
        {
            return TranslationMessage.Of(RegulationKeys.UnknownTeam);
        }

        if (series.ItemOf(itemId) is not { } item)
        {
            return TranslationMessage.Of(RegulationKeys.UnknownItem);
        }

        if (item.IsResolved || today > item.Deadline || today < item.Announced)
        {
            return TranslationMessage.Of(RegulationKeys.ItemClosed);
        }

        if (option is not (BallotOptions.StatusQuo or BallotOptions.Abstain) && item.VariantOf(option) is null)
        {
            return TranslationMessage.Of(RegulationKeys.UnknownOption);
        }

        if (spent < 0 || (spent > 0 && _environment.Mode != VoteMode.VoteBank))
        {
            return TranslationMessage.Of(RegulationKeys.SpendNeedsBank);
        }

        if (spent > 0 && option == BallotOptions.Abstain)
        {
            return TranslationMessage.Of(RegulationKeys.SpendOnAbstain);
        }

        if (spent > RegulationEstimates.MaxSpendPerItem)
        {
            return TranslationMessage.Of(RegulationKeys.SpendTooMany, ("max", RegulationEstimates.MaxSpendPerItem.ToString(CultureInfo.InvariantCulture)));
        }

        var committed = 0;
        foreach (var other in series.Ballot)
        {
            if (!other.IsResolved && other.Id != itemId && other.Votes.FirstOrDefault(vote => vote.TeamId == teamId) is { } vote)
            {
                committed += vote.Spent;
            }
        }

        var free = politics.Bank - committed;
        if (spent > free)
        {
            return TranslationMessage.Of(RegulationKeys.SpendOverBank, ("bank", Math.Max(0, free).ToString(CultureInfo.InvariantCulture)));
        }

        if (option == BallotOptions.Abstain && _environment.Mode == VoteMode.VoteBank && politics.Bank >= RegulationEstimates.BankCap)
        {
            return TranslationMessage.Of(RegulationKeys.BankFull, ("cap", RegulationEstimates.BankCap.ToString(CultureInfo.InvariantCulture)));
        }

        return null;
    }

    /// <summary>Stores the team's vote on the item (it replaces an earlier one). The caller has checked it with <see cref="CheckCast"/>.</summary>
    public void Cast(string seriesId, string teamId, string itemId, string option, int spent, GameDate today)
    {
        var reason = CheckCast(seriesId, teamId, itemId, option, spent, today);
        if (reason is not null)
        {
            throw new InvalidOperationException("The vote was refused: " + reason.Key + ".");
        }

        var section = _book.Section!;
        var series = section.Find(seriesId)!;
        var item = series.ItemOf(itemId)!;
        var votes = item.Votes.Where(vote => vote.TeamId != teamId).Append(new CastVote(teamId, option, spent)).ToArray();
        _book.Write(section.WithSeries(series.WithItem(item.WithVotes(votes))));
    }

    // ---------------------------------------------------------------- the day

    /// <summary>
    /// One lived day of the political life of every series: the season turns, new teams join, AI teams file proposals in the window,
    /// the teams' ballot is built when the window closes, the FIA's votes are announced on their days, and every ballot item whose
    /// deadline has come is counted and stored for the next season. A quiet day writes nothing.
    /// </summary>
    public void OnDay(GameDate today)
    {
        if (!_environment.Votes || _book.Section is not { } section)
        {
            return;
        }

        var finance = _book.Finance;
        var changed = false;
        foreach (var seriesId in _environment.Series.SeriesIds)
        {
            var series = section.Find(seriesId);
            if (series is null)
            {
                continue;
            }

            var before = series;
            var financeBefore = finance;
            series = Step(series, ref finance, today);
            if (!ReferenceEquals(series, before) || !ReferenceEquals(finance, financeBefore))
            {
                section = section.WithSeries(series);
                changed = true;
            }
        }

        if (changed)
        {
            _book.Write(section, finance);
        }
    }

    private SeriesRegulations Step(SeriesRegulations series, ref FinanceSection finance, GameDate today)
    {
        var season = today.Year;
        while (series.Season < season)
        {
            series = series.Promote();
        }

        var teams = _environment.Series.TeamsOf(series.SeriesId, _book.World, today);
        series = EnsureTeams(series, teams, season, opening: false);

        if (series.AgendaSeason != season)
        {
            series = series.WithAgenda(season, 0);
        }

        if (today >= RegulationSchedule.ProposalsOpen(season) && today <= RegulationSchedule.ProposalsClose(season))
        {
            series = AiProposals(series, ref finance, teams, today);
        }

        if (series.TeamBallotSeason < season && today >= RegulationSchedule.TeamBallotAnnounced(season))
        {
            series = BuildTeamBallot(series, today);
        }

        if (!_fiaDays.TryGetValue((series.SeriesId, season), out var days))
        {
            days = RegulationSchedule.FiaAnnouncementDays(season, RegulationSchedule.FiaVotesIn(_environment.MasterSeed, series.SeriesId, season));
            _fiaDays[(series.SeriesId, season)] = days;
        }

        var slots = days.Count;
        while (series.FiaSlotsDone < slots && today >= days[series.FiaSlotsDone])
        {
            series = AnnounceFia(series, series.FiaSlotsDone, finance, teams, today);
        }

        foreach (var item in series.Ballot.Where(item => !item.IsResolved && item.Deadline <= today).OrderBy(item => item.Deadline).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray())
        {
            series = Resolve(series, series.ItemOf(item.Id)!, finance, teams, today);
        }

        return series;
    }

    private SeriesRegulations AiProposals(SeriesRegulations series, ref FinanceSection finance, IReadOnlyList<Organization> teams, GameDate today)
    {
        var season = today.Year;

        // Another team's pending proposal on the same dimension is no reason to stay away: they merge into one ballot item.
        var onBallot = new HashSet<string>(series.Ballot.Where(item => item.Season == season).Select(item => item.DimensionId), StringComparer.Ordinal);
        foreach (var team in teams)
        {
            if (IsHumanSeat(team.Id) || series.TeamOf(team.Id.Value) is not { } politics || season < politics.ProposeFromSeason
                || series.Pending.Any(pending => pending.TeamId == team.Id.Value)
                || RegulationSchedule.AiConsiderationDay(_environment.MasterSeed, series.SeriesId, season, team.Id.Value) != today
                || !finance.HasBook(team.Id))
            {
                continue;
            }

            var table = RegulationFacts.TableOf(_book.World, season);
            var positions = RegulationFacts.Positions(table, teams.Select(candidate => candidate.Id.Value).ToArray());
            var view = RegulationFacts.ViewOf(finance, table, positions, team, season, _environment.CountryOf(team.Id.Value));
            var basis = ProposalFee.RevenueBasis(finance.EntriesOf(team.Id), season);
            var fee = ProposalFee.Quote(basis, finance.TypicalCents);
            var options = ChoicesFor(series, season, onBallot).Select(choice => OptionFor(choice, team.Id.Value)).ToArray();
            var decision = AiPolitics.Consider(new AiProposalInputs(
                view,
                TeamLeaningSeam.Of(series, team.Id.Value, season),
                fee,
                finance.BalanceOf(team.Id),
                basis,
                options));
            if (!decision.Propose || decision.Choice is not { } choice)
            {
                continue;
            }

            series = Propose(series, ref finance, team.Id.Value, choice.DimensionId, choice.Value, today, out _);
        }

        return series;
    }

    private SeriesRegulations BuildTeamBallot(SeriesRegulations series, GameDate today)
    {
        var season = today.Year;
        var announced = RegulationSchedule.TeamBallotAnnounced(season);
        var deadline = RegulationSchedule.DeadlineOf(announced);
        var next = (series.NextValues ?? series.Values).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var nextPolicy = (series.NextCalendar ?? series.Calendar).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var created = new List<BallotItem>();
        foreach (var group in series.Pending.GroupBy(pending => pending.DimensionId).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(pending => pending.Filed).ThenBy(pending => pending.Value, StringComparer.Ordinal).ThenBy(pending => pending.TeamId, StringComparer.Ordinal).ToArray();
            var variants = new List<BallotVariant>();
            foreach (var byValue in ordered.GroupBy(pending => pending.Value))
            {
                variants.Add(new BallotVariant(BallotOptions.VariantId(variants.Count + 1), byValue.Key, [.. byValue.Select(pending => pending.TeamId)]));
            }

            var current = CalendarPolicy.IsCalendarDimension(group.Key)
                ? (nextPolicy.TryGetValue(group.Key, out var held) ? held : CalendarPolicy.Kept)
                : (next.TryGetValue(group.Key, out var ruleValue) ? ruleValue : variants[0].Value);
            var item = new BallotItem(
                BallotId(series.SeriesId, season, group.Key),
                season,
                group.Key,
                BallotOrigin.Teams,
                announced,
                deadline,
                current,
                RegulationKeys.ReasonTeamProposal,
                [new KeyValuePair<string, string>("proposers", string.Join(",", variants.SelectMany(variant => variant.ProposerTeamIds).OrderBy(id => id, StringComparer.Ordinal)))],
                variants,
                [],
                null);
            series = series.WithItem(item);
            created.Add(item);
        }

        series = series.WithTeamBallotSeason(season).WithPending([]);
        foreach (var item in created)
        {
            Announce(series, item, today);
        }

        return series;
    }

    private SeriesRegulations AnnounceFia(SeriesRegulations series, int slot, FinanceSection finance, IReadOnlyList<Organization> teams, GameDate today)
    {
        var season = today.Year;
        var onBallot = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in series.Ballot)
        {
            if (item.Season == season)
            {
                onBallot.Add(item.DimensionId);
            }
        }

        var choices = ChoicesFor(series, season, onBallot);
        if (choices.Count == 0)
        {
            choices = ChoicesFor(series, season, onBallot, ignoreMemory: true);
        }

        var table = RegulationFacts.TableOf(_book.World, season);
        var pressures = RegulationFacts.PressuresOf(table, finance, teams);
        var rng = RegulationSchedule.Child(_environment.MasterSeed, season, "fia:" + series.SeriesId + ":" + slot.ToString(CultureInfo.InvariantCulture));
        var proposal = FiaAgenda.Propose(pressures, [.. choices.Select(choice => new RuleCandidate(choice.DimensionId, choice.Current, choice.Value, choice.Delta))], rng);
        series = series.WithAgenda(season, slot + 1);
        if (proposal is null)
        {
            return series;
        }

        var announced = today;
        var current = choices.First(choice => choice.DimensionId == proposal.DimensionId).Current;
        var made = new BallotItem(
            BallotId(series.SeriesId, season, proposal.DimensionId),
            season,
            proposal.DimensionId,
            BallotOrigin.Fia,
            announced,
            RegulationSchedule.DeadlineOf(announced),
            current,
            proposal.ReasonKey,
            proposal.ReasonArguments,
            [new BallotVariant(BallotOptions.VariantId(1), proposal.Value, [])],
            [],
            null);
        series = series.WithItem(made);
        Announce(series, made, today);
        return series;
    }

    private static string BallotId(string seriesId, int season, string dimensionId) =>
        seriesId + ":" + season.ToString(CultureInfo.InvariantCulture) + ":" + dimensionId;

    // ---------------------------------------------------------------- counting

    private SeriesRegulations Resolve(SeriesRegulations series, BallotItem item, FinanceSection finance, IReadOnlyList<Organization> teams, GameDate today)
    {
        var body = VotingBodyDefaults.ForSeason(item.Season);
        var table = RegulationFacts.TableOf(_book.World, item.Season);
        var teamIds = teams.Where(team => series.TeamOf(team.Id.Value) is not null).Select(team => team.Id.Value).ToArray();
        var positions = RegulationFacts.Positions(table, teamIds);
        var ballots = new List<VoterBallot>(teams.Count);
        var bank = teamIds.ToDictionary(id => id, id => series.TeamOf(id)!.Bank, StringComparer.Ordinal);
        foreach (var team in teams.OrderBy(team => team.Id.Value, StringComparer.Ordinal))
        {
            var teamId = team.Id.Value;
            if (series.TeamOf(teamId) is not { } politics)
            {
                continue;
            }

            var weight = body.Weighting == VoteWeighting.ByChampionshipPosition ? Math.Max(1, teamIds.Length - positions[teamId] + 1) : 1;
            var cast = item.Votes.FirstOrDefault(vote => vote.TeamId == teamId);
            VoterBallot ballot;
            if (cast is not null)
            {
                ballot = new VoterBallot(teamId, cast.Option, weight, cast.Spent, cast.Option == BallotOptions.Abstain && _environment.Mode == VoteMode.VoteBank && politics.Bank < RegulationEstimates.BankCap, "");
            }
            else if (IsHumanSeat(team.Id))
            {
                ballot = new VoterBallot(teamId, BallotOptions.Abstain, weight, 0, _environment.Mode == VoteMode.VoteBank && politics.Bank < RegulationEstimates.BankCap, "");
            }
            else
            {
                var view = RegulationFacts.ViewOf(finance, table, positions, team, item.Season, _environment.CountryOf(teamId));
                var rng = RegulationSchedule.Child(_environment.MasterSeed, item.Season, "vote:" + item.Id + ":" + teamId);
                var variants = item.Variants
                    .Select(variant => (variant.Id, Option: OptionFor(ChoiceOf(item, variant), teamId), Noise: ((rng.NextDouble() * 2) - 1) * RegulationEstimates.VoteNoise))
                    .ToArray();
                var vote = AiPolitics.Vote(new AiVoteInputs(view, TeamLeaningSeam.Of(series, teamId, item.Season), variants, _environment.Mode, politics.Bank));
                ballot = new VoterBallot(teamId, vote.Option, weight, vote.Spent, vote.Banked, vote.ReasonKey);
            }

            ballots.Add(ballot);
        }

        var result = BallotTally.Resolve(
            [.. item.Variants.Select(variant => variant.Id)],
            ballots,
            body.Threshold,
            tied => FiaPresident.Choose(item.Origin, tied, option => ChoiceOf(item, item.VariantOf(option)!).Delta));
        foreach (var ballot in ballots)
        {
            var politics = series.TeamOf(ballot.TeamId)!;
            var kept = Math.Max(0, politics.Bank - ballot.Spent) + (ballot.Banked ? 1 : 0);
            series = series.WithTeam(politics.WithBank(Math.Min(kept, Math.Max(RegulationEstimates.BankCap, politics.Bank))));
        }

        var nextSeason = item.Season + 1;
        var adopted = result.Outcome == BallotOutcome.Adopted ? item.VariantOf(result.WinningOption) : null;
        if (adopted is not null)
        {
            var applied = Apply(series, item, adopted.Value, nextSeason);
            if (applied.Series is { } changed)
            {
                series = changed;
            }
            else
            {
                result = new BallotResult(BallotOutcome.Rejected, result.WinningOption, applied.ReasonKey, result.PresidentDecided, result.Tally, result.Stances);
                adopted = null;
            }
        }

        if (result.Outcome != BallotOutcome.NoVotes)
        {
            var memory = series.Rejected.Where(rejected => nextSeason - rejected.Season <= RegulationEstimates.RejectionMemorySeasons).ToList();
            foreach (var variant in item.Variants)
            {
                if (adopted is not null && variant.Id == adopted.Id)
                {
                    continue;
                }

                if (!memory.Any(rejected => rejected.DimensionId == item.DimensionId && rejected.Value == variant.Value && rejected.Season == nextSeason))
                {
                    memory.Add(new RejectedRegulation(item.DimensionId, variant.Value, nextSeason));
                }
            }

            series = series.WithRejected(memory);
        }

        var resolved = item.WithResult(result);
        series = series.WithItem(resolved);
        NoticeResult(series, resolved, adopted);
        return series;
    }

    private RuleChoice ChoiceOf(BallotItem item, BallotVariant variant)
    {
        if (CalendarPolicy.IsCalendarDimension(item.DimensionId))
        {
            var circuit = CalendarPolicy.CircuitOf(item.DimensionId);
            var option = new CalendarPolicy.Option(
                item.DimensionId,
                circuit,
                CountryOfCircuit(circuit),
                IsAuthored(circuit, item.Season + 1),
                item.CurrentValue,
                [variant.Value]);
            return new RuleChoice(item.DimensionId, item.CurrentValue, variant.Value, CalendarPolicy.TraitsOf(variant.Value) - CalendarPolicy.TraitsOf(item.CurrentValue), option);
        }

        return new RuleChoice(
            item.DimensionId,
            item.CurrentValue,
            variant.Value,
            LiveRules.TraitsOf(item.DimensionId, variant.Value) - LiveRules.TraitsOf(item.DimensionId, item.CurrentValue),
            null);
    }

    private string CountryOfCircuit(string circuitId) =>
        _environment.Layouts.FirstOrDefault(layout => layout.CircuitId == circuitId)?.Country ?? "";

    private bool IsAuthored(string circuitId, int season)
    {
        var layouts = _environment.Layouts.Where(layout => layout.CircuitId == circuitId).Select(layout => layout.Id).ToHashSet(StringComparer.Ordinal);
        return _environment.Assignments.Any(assignment => assignment.Season == season && layouts.Contains(assignment.LayoutId));
    }

    /// <summary>
    /// Puts an adopted variant into the rules or the calendar voted for the next season. A value the catalog does not accept, or a
    /// calendar that would keep too few rounds, is refused with a reason instead of being stored: nothing fails silently.
    /// </summary>
    private (SeriesRegulations? Series, string ReasonKey) Apply(SeriesRegulations series, BallotItem item, string value, int nextSeason)
    {
        var values = (series.NextValues ?? series.Values).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var policy = (series.NextCalendar ?? series.Calendar).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (CalendarPolicy.IsCalendarDimension(item.DimensionId))
        {
            if (!CalendarPolicy.IsKnownValue(value)
                || !CalendarPolicy.KeepsEnoughRounds(nextSeason, _environment.Layouts, _environment.Assignments, policy, item.DimensionId, value))
            {
                return (null, RegulationKeys.ResultCalendarTooShort);
            }

            if (value == CalendarPolicy.Kept)
            {
                policy.Remove(item.DimensionId);
            }
            else
            {
                policy[item.DimensionId] = value;
            }
        }
        else
        {
            try
            {
                var rules = RuleSet.Restore(series.Season, values).With(_environment.Specs, nextSeason, [new RuleChange(item.DimensionId, value)]);
                values = rules.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            }
            catch (RuleChangeException)
            {
                return (null, RegulationKeys.ResultInvalid);
            }
        }

        return (series.WithNext(values, policy), "");
    }

    // ---------------------------------------------------------------- notices

    private void Announce(SeriesRegulations series, BallotItem item, GameDate today)
    {
        if (_inbox is null)
        {
            return;
        }

        foreach (var team in _environment.Series.TeamsOf(series.SeriesId, _book.World, today))
        {
            foreach (var manager in _environment.HumansOf(team.Id))
            {
                var draft = new InboxItemDraft(
                    RegulationKeys.AnnouncedKind,
                    RegulationKeys.AnnouncedSubject,
                    [
                        new KeyValuePair<string, string>("series", series.SeriesId),
                        new KeyValuePair<string, string>("item", item.Id),
                        new KeyValuePair<string, string>("dimension", item.DimensionId),
                        new KeyValuePair<string, string>("current", item.CurrentValue),
                        new KeyValuePair<string, string>("variants", string.Join(",", item.Variants.Select(variant => variant.Value))),
                        new KeyValuePair<string, string>("origin", item.Origin == BallotOrigin.Fia ? "fia" : "teams"),
                        new KeyValuePair<string, string>("deadline", item.Deadline.ToString()),
                        new KeyValuePair<string, string>("reason", item.ReasonKey),
                        new KeyValuePair<string, string>("effectiveSeason", (item.Season + 1).ToString(CultureInfo.InvariantCulture)),
                    ],
                    options: null,
                    validUntil: null,
                    defaultOptionId: null);
                _inbox.Post(_environment.Managers, manager, draft, today);
            }
        }
    }

    private void NoticeResult(SeriesRegulations series, BallotItem item, BallotVariant? adopted)
    {
        if (_inbox is null || item.Result is not { } result)
        {
            return;
        }

        var today = item.Deadline;
        foreach (var team in _environment.Series.TeamsOf(series.SeriesId, _book.World, today))
        {
            foreach (var manager in _environment.HumansOf(team.Id))
            {
                var draft = new InboxItemDraft(
                    RegulationKeys.ResultKind,
                    RegulationKeys.ResultSubject,
                    [
                        new KeyValuePair<string, string>("series", series.SeriesId),
                        new KeyValuePair<string, string>("item", item.Id),
                        new KeyValuePair<string, string>("dimension", item.DimensionId),
                        new KeyValuePair<string, string>("outcome", result.Outcome.ToString()),
                        new KeyValuePair<string, string>("value", adopted?.Value ?? item.CurrentValue),
                        new KeyValuePair<string, string>("reason", result.ReasonKey),
                        new KeyValuePair<string, string>("effectiveSeason", (item.Season + 1).ToString(CultureInfo.InvariantCulture)),
                    ],
                    options: null,
                    validUntil: null,
                    defaultOptionId: null);
                _inbox.Post(_environment.Managers, manager, draft, today);
            }
        }
    }
}
