using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Objectives;
using Paddock.Domain.Board;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Board;

public enum BoardForecastKind
{
    /// <summary>No review has happened yet, so there is nothing to project from.</summary>
    Unknown,

    /// <summary>The principal cannot be dismissed yet.</summary>
    Protected,

    /// <summary>At the current form the board's confidence stays above its bar.</summary>
    Safe,

    /// <summary>At the current form the board dismisses after <see cref="BoardForecastView.ReviewsLeft"/> more reviews.</summary>
    AtRisk,
}

/// <summary>One change of the reputation, in whole points (the sign says the direction), with its reason as a key.</summary>
public sealed record ReputationLineView(DateOnly On, int Points, TranslationMessage Reason);

/// <summary>The reputation of a manager in whole points (PP-013), as a band, and the last changes.</summary>
public sealed record ReputationView(int Points, TranslationMessage Band, IReadOnlyList<ReputationLineView> History);

/// <summary>STATE: where the board stands. Confidence is in whole points.</summary>
public sealed record BoardStateView(
    int Confidence,
    TranslationMessage Band,
    bool Protected,
    DateOnly? ProtectedUntil,
    int ReviewsBelowBar,
    int ReviewsToDismiss);

/// <summary>WHY: what the board expects (the objectives it granted) and what its last review read.</summary>
public sealed record BoardWhyView(IReadOnlyList<ObjectiveItemView> Expectations, int? ExpectedPosition, decimal? CurrentPosition, int? TargetConfidence);

/// <summary>FORECAST: the straight projection of the last review forward (ESTIMATE; no RNG).</summary>
public sealed record BoardForecastView(BoardForecastKind Kind, int? ReviewsLeft, TranslationMessage Message);

public sealed record OwnBoardView(string OrganizationId, BoardStateView State, BoardWhyView Why, BoardForecastView Forecast);

/// <summary>A manager without a team watches the world as a neutral observer: no team view, only reputation and offers.</summary>
public sealed record ObserverView(TranslationMessage Status, DateOnly Since, int OpenOffers);

/// <summary>What the developer sees of a board, truth included.</summary>
public sealed record DeveloperBoardLine(string OrganizationId, PrincipalKind? Kind, string? Principal, int ConfidenceTenths, int LowStreak, int Patience, string Archetype);

public sealed record BoardView(
    AccessContext Viewer,
    OwnBoardView? Own,
    ObserverView? Observer,
    ReputationView? Reputation,
    IReadOnlyList<DeveloperBoardLine> Boards);

/// <summary>
/// The read side of the board (INV-003, INV-005): STATE, WHY and FORECAST of the manager's own board, and their own reputation.
/// A manager sees only the board of the team they run and their own reputation; the board of another team, the archetype of an
/// AI principal and the confidence of any other board are not in the view. A dismissed manager gets the observer view and no team.
/// The developer sees every board. It changes nothing and draws no RNG.
/// </summary>
public sealed class BoardQuery
{
    private const int HistoryLines = 5;

    private readonly BoardBook _book;
    private readonly InboxBook _inbox;
    private readonly ObjectiveQuery _objectives;

    public BoardQuery(BoardBook book, InboxBook inbox, ObjectiveQuery objectives)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(objectives);
        _book = book;
        _inbox = inbox;
        _objectives = objectives;
    }

    public BoardView View(AccessContext access, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(access);
        var section = _book.Section;
        if (access.Kind == AccessKind.Developer)
        {
            var lines = section.Boards.Select(board => new DeveloperBoardLine(
                board.Organization.Value,
                board.Principal?.Kind,
                board.Principal?.Subject,
                board.ConfidenceTenths,
                board.LowStreak,
                board.Patience,
                board.Archetype)).ToArray();
            return new BoardView(access, null, null, null, lines);
        }

        var manager = access.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.");
        var reputation = ReputationOf(section, manager.Value);
        if (section.OrganizationOf(manager.Value) is OrganizationId own && section.Board(own) is BoardRecord board)
        {
            return new BoardView(access, Describe(access, board, today), null, reputation, []);
        }

        if (section.UnemployedManager(manager.Value) is UnemployedRecord record)
        {
            var open = _inbox.Section.ItemsOf(manager.Value).Count(item => item.IsOpen && item.Kind == BoardEngine.OfferKind);
            return new BoardView(
                access,
                null,
                new ObserverView(TranslationMessage.Of(BoardKeys.ObserverStatus), ToDate(record.Since), open),
                reputation,
                []);
        }

        return new BoardView(access, null, null, null, []);
    }

    /// <summary>The number of whole points a stored value in tenths shows as.</summary>
    public static int Points(int tenths) => (int)Math.Round(tenths / 10.0, MidpointRounding.AwayFromZero);

    private OwnBoardView Describe(AccessContext access, BoardRecord board, GameDate today)
    {
        var principal = board.Principal!;
        var threshold = ReputationModel.DismissThresholdTenths(board.Patience);
        var needed = ReputationModel.ReviewsToDismiss(board.Patience);
        var protectedNow = principal.Founder || today <= principal.ProtectedUntil;
        var state = new BoardStateView(
            Points(board.ConfidenceTenths),
            TranslationMessage.Of(BandKey(board.ConfidenceTenths, threshold)),
            protectedNow,
            principal.Founder ? null : ToDate(principal.ProtectedUntil),
            board.LowStreak,
            needed);
        var objectives = _objectives.View(access, _book.Objectives, today).Items
            .Where(item => item.Title.Key is BoardKeys.ObjectiveSeason or BoardKeys.ObjectiveMultiYear)
            .ToArray();
        var position = _book.Facts.Number(board.Organization, Paddock.Domain.Objectives.ObjectiveFactKeys.ChampionshipPosition);
        var why = new BoardWhyView(
            objectives,
            board.ExpectedPosition > 0 ? board.ExpectedPosition : null,
            position,
            board.LastTargetTenths is int target ? Points(target) : null);
        return new OwnBoardView(board.Organization.Value, state, why, Forecast(board, protectedNow, threshold, needed));
    }

    private static BoardForecastView Forecast(BoardRecord board, bool protectedNow, int threshold, int needed)
    {
        if (protectedNow)
        {
            return new BoardForecastView(BoardForecastKind.Protected, null, TranslationMessage.Of(BoardKeys.ForecastProtected));
        }

        if (board.LastTargetTenths is not int target)
        {
            return new BoardForecastView(BoardForecastKind.Unknown, null, TranslationMessage.Of(BoardKeys.ForecastUnknown));
        }

        if (target >= threshold)
        {
            return new BoardForecastView(BoardForecastKind.Safe, null, TranslationMessage.Of(BoardKeys.ForecastSafe));
        }

        // The projection: the same review rule, repeated with today's target and no cash penalty.
        var confidence = board.ConfidenceTenths;
        var streak = board.LowStreak;
        for (var reviews = 1; reviews <= 200; reviews++)
        {
            confidence = ReputationModel.ReviewConfidence(confidence, target, 0);
            streak = confidence < threshold ? streak + 1 : 0;
            if (streak >= needed)
            {
                return new BoardForecastView(
                    BoardForecastKind.AtRisk,
                    reviews,
                    TranslationMessage.Of(BoardKeys.ForecastAtRisk, ("reviews", reviews.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            }
        }

        return new BoardForecastView(BoardForecastKind.Safe, null, TranslationMessage.Of(BoardKeys.ForecastSafe));
    }

    private static string BandKey(int confidenceTenths, int thresholdTenths)
    {
        if (confidenceTenths >= 700)
        {
            return BoardKeys.ConfidenceSecure;
        }

        if (confidenceTenths >= 500)
        {
            return BoardKeys.ConfidenceSteady;
        }

        return confidenceTenths >= thresholdTenths ? BoardKeys.ConfidenceShaky : BoardKeys.ConfidenceDanger;
    }

    private static ReputationView ReputationOf(BoardSection section, string manager)
    {
        var tenths = section.ReputationTenths(manager);
        var band = tenths switch
        {
            < 200 => BoardKeys.BandUnknown,
            < 400 => BoardKeys.BandModest,
            < 600 => BoardKeys.BandRespected,
            < 800 => BoardKeys.BandRenowned,
            _ => BoardKeys.BandLegendary,
        };
        var history = section.ChangesOf(manager)
            .TakeLast(HistoryLines)
            .Select(change => new ReputationLineView(ToDate(change.On), Points(change.DeltaTenths), TranslationMessage.Of(change.ReasonKey)))
            .ToArray();
        return new ReputationView(Points(tenths), TranslationMessage.Of(band), history);
    }

    private static DateOnly ToDate(GameDate date) => new(date.Year, date.Month, date.Day);
}
