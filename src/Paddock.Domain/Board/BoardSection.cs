using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Board;

public enum PrincipalKind
{
    /// <summary>A manager (a player) runs the team. The subject is a manager id.</summary>
    Human,

    /// <summary>The team's own principal, a person of the world. The subject is a person id.</summary>
    Ai,
}

/// <summary>
/// Who runs a team. <see cref="ProtectedUntil"/> is the last day the board cannot dismiss them (PP-050). A <see cref="Founder"/>
/// of the team is never dismissed (the founder case itself is post-MVP; the rule is here so it cannot be forgotten).
/// </summary>
public sealed record PrincipalRecord(PrincipalKind Kind, string Subject, GameDate Since, GameDate ProtectedUntil, bool Founder);

/// <summary>
/// The board of one organization: a non-person entity with a patience scalar and no attributes (PP-050, ROADMAP open question 1).
/// <see cref="ExpectedPosition"/> is 0 until the season's expectation is set. <see cref="LastTargetTenths"/> and
/// <see cref="LastCash"/> are what the last review read, kept so the view can say why.
/// </summary>
public sealed record BoardRecord(
    OrganizationId Organization,
    int Patience,
    int ConfidenceTenths,
    int LowStreak,
    int ExpectedPosition,
    string Archetype,
    PrincipalRecord? Principal,
    GameDate? LastReview,
    int? LastTargetTenths,
    long? LastCash);

/// <summary>A manager with no team. They watch the world with no team view until they accept a job (PP-050).</summary>
public sealed record UnemployedRecord(
    string Manager,
    GameDate Since,
    OrganizationId? FormerOrganization,
    long Severance,
    int OffersMade,
    GameDate? LastOfferOn);

/// <summary>One change of a reputation, with the reason as a translation key.</summary>
public sealed record ReputationChange(string Subject, GameDate On, int DeltaTenths, int ResultTenths, string ReasonKey);

/// <summary>
/// The board world section, named <see cref="SectionName"/> (T45): the board of each team, who runs it, the reputation of every
/// principal with its history, and the managers without a team. Immutable: every change returns a new section.
/// Holds truth (the board's confidence, the AI principal's archetype); a manager reads it only through the board query (INV-003).
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash:
/// <code>
/// boards &lt;count&gt;
/// board &lt;len&gt;:&lt;organizationId&gt; &lt;patience&gt; &lt;confidenceTenths&gt; &lt;lowStreak&gt; &lt;expected&gt; &lt;len&gt;:&lt;archetype&gt;
/// review &lt;len&gt;:&lt;date or -&gt; &lt;len&gt;:&lt;targetTenths or -&gt; &lt;len&gt;:&lt;cash or -&gt;
/// principal &lt;len&gt;:&lt;Human|Ai or -&gt; &lt;len&gt;:&lt;subject&gt; &lt;len&gt;:&lt;since&gt; &lt;len&gt;:&lt;protectedUntil&gt; &lt;0|1&gt;
/// reputations &lt;count&gt;
/// reputation &lt;len&gt;:&lt;subject&gt; &lt;tenths&gt;
/// changes &lt;count&gt;
/// change &lt;len&gt;:&lt;subject&gt; &lt;len&gt;:&lt;date&gt; &lt;delta&gt; &lt;result&gt; &lt;len&gt;:&lt;reasonKey&gt;
/// unemployed &lt;count&gt;
/// manager &lt;len&gt;:&lt;id&gt; &lt;len&gt;:&lt;since&gt; &lt;len&gt;:&lt;formerOrganization or -&gt; &lt;severance&gt; &lt;offersMade&gt; &lt;len&gt;:&lt;lastOffer or -&gt;
/// </code>
/// </para>
/// </summary>
public sealed class BoardSection : IWorldSection
{
    public const string SectionName = "board";

    private const string None = "-";

    private readonly SortedDictionary<string, BoardRecord> _boards;
    private readonly SortedDictionary<string, int> _reputation;
    private readonly IReadOnlyList<ReputationChange> _changes;
    private readonly SortedDictionary<string, UnemployedRecord> _unemployed;

    private BoardSection(
        SortedDictionary<string, BoardRecord> boards,
        SortedDictionary<string, int> reputation,
        IReadOnlyList<ReputationChange> changes,
        SortedDictionary<string, UnemployedRecord> unemployed)
    {
        _boards = boards;
        _reputation = reputation;
        _changes = changes;
        _unemployed = unemployed;
    }

    public static BoardSection Empty { get; } = new(NewMap<BoardRecord>(), NewMap<int>(), [], NewMap<UnemployedRecord>());

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _boards.Count == 0 && _reputation.Count == 0 && _unemployed.Count == 0;

    /// <summary>The boards, in ordinal order of the organization id.</summary>
    public IReadOnlyList<BoardRecord> Boards => _boards.Values.ToArray();

    public IReadOnlyList<UnemployedRecord> Unemployed => _unemployed.Values.ToArray();

    /// <summary>Every reputation change, oldest first.</summary>
    public IReadOnlyList<ReputationChange> Changes => _changes;

    /// <summary>Every stored reputation as (subject, tenths), in ordinal order of the subject.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> Reputations => _reputation.ToArray();

    public static BoardSection Restore(
        IEnumerable<BoardRecord> boards,
        IEnumerable<KeyValuePair<string, int>> reputations,
        IEnumerable<ReputationChange> changes,
        IEnumerable<UnemployedRecord> unemployed)
    {
        ArgumentNullException.ThrowIfNull(boards);
        ArgumentNullException.ThrowIfNull(reputations);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(unemployed);
        var section = Empty;
        foreach (var board in boards)
        {
            if (section._boards.ContainsKey(board.Organization.Value))
            {
                throw new InvalidOperationException($"Organization '{board.Organization}' has two boards.");
            }

            section = section.WithBoard(board);
        }

        var map = NewMap<int>();
        foreach (var (subject, tenths) in reputations)
        {
            if (tenths is < 0 or > BoardEstimates.MaxTenths || !map.TryAdd(subject, tenths))
            {
                throw new InvalidOperationException($"Reputation of '{subject}' is stored twice or out of range.");
            }
        }

        var people = NewMap<UnemployedRecord>();
        foreach (var record in unemployed)
        {
            if (!people.TryAdd(record.Manager, record))
            {
                throw new InvalidOperationException($"Manager '{record.Manager}' is unemployed twice.");
            }
        }

        return new BoardSection(section._boards, map, changes.ToArray(), people);
    }

    public BoardRecord? Board(OrganizationId organization) =>
        organization.IsAssigned && _boards.TryGetValue(organization.Value, out var board) ? board : null;

    public BoardSection WithBoard(BoardRecord board)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (board.ConfidenceTenths is < 0 or > BoardEstimates.MaxTenths || board.Patience is < 0 or > 100 || board.LowStreak < 0 || board.ExpectedPosition < 0)
        {
            throw new ArgumentException("A board record is out of range.", nameof(board));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(board.Archetype);
        var boards = new SortedDictionary<string, BoardRecord>(_boards, StringComparer.Ordinal) { [board.Organization.Value] = board };
        return new BoardSection(boards, _reputation, _changes, _unemployed);
    }

    /// <summary>The organization a manager runs as principal, or null.</summary>
    public OrganizationId? OrganizationOf(string manager)
    {
        foreach (var board in _boards.Values)
        {
            if (board.Principal is { Kind: PrincipalKind.Human } principal && principal.Subject == manager)
            {
                return board.Organization;
            }
        }

        return null;
    }

    /// <summary>The reputation of a manager or an AI principal in tenths, the initial one for a subject with none yet.</summary>
    public int ReputationTenths(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return _reputation.TryGetValue(subject, out var tenths) ? tenths : BoardEstimates.InitialReputationTenths;
    }

    /// <summary>The reputation of the principal of an organization in tenths, or null when it has none.</summary>
    public int? ReputationOfPrincipal(OrganizationId organization) =>
        Board(organization)?.Principal is PrincipalRecord principal ? ReputationTenths(principal.Subject) : null;

    public IReadOnlyList<ReputationChange> ChangesOf(string subject) =>
        _changes.Where(change => change.Subject == subject).ToArray();

    /// <summary>Changes a reputation, keeping it between 0 and 100, and writes the history line. A change of zero is not recorded.</summary>
    public BoardSection WithReputationChange(string subject, GameDate on, int deltaTenths, string reasonKey)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        var before = ReputationTenths(subject);
        var after = BoardEstimates.Clamp(before + deltaTenths);
        if (after == before)
        {
            return _reputation.ContainsKey(subject) ? this : WithReputation(subject, after, _changes);
        }

        var changes = _changes.Append(new ReputationChange(subject, on, after - before, after, reasonKey)).ToArray();
        return WithReputation(subject, after, changes);
    }

    /// <summary>Sets a reputation without a history line, for a subject first met (a new principal arrives with a reputation).</summary>
    public BoardSection WithInitialReputation(string subject, int tenths)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return _reputation.ContainsKey(subject) ? this : WithReputation(subject, BoardEstimates.Clamp(tenths), _changes);
    }

    public UnemployedRecord? UnemployedManager(string manager) =>
        _unemployed.TryGetValue(manager, out var record) ? record : null;

    public BoardSection WithUnemployed(UnemployedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var map = new SortedDictionary<string, UnemployedRecord>(_unemployed, StringComparer.Ordinal) { [record.Manager] = record };
        return new BoardSection(_boards, _reputation, _changes, map);
    }

    public BoardSection WithoutUnemployed(string manager)
    {
        var map = new SortedDictionary<string, UnemployedRecord>(_unemployed, StringComparer.Ordinal);
        return map.Remove(manager) ? new BoardSection(_boards, _reputation, _changes, map) : this;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("boards", _boards.Count);
        foreach (var board in _boards.Values)
        {
            writer.Begin("board");
            writer.Field(board.Organization.Value);
            writer.Space();
            writer.Raw(Number(board.Patience) + " " + Number(board.ConfidenceTenths) + " " + Number(board.LowStreak) + " " + Number(board.ExpectedPosition) + " ");
            writer.Field(board.Archetype);
            writer.End();
            writer.Begin("review");
            writer.Field(board.LastReview?.ToString() ?? None);
            writer.Space();
            writer.Field(board.LastTargetTenths is int target ? Number(target) : None);
            writer.Space();
            writer.Field(board.LastCash is long cash ? cash.ToString(CultureInfo.InvariantCulture) : None);
            writer.End();
            writer.Begin("principal");
            if (board.Principal is PrincipalRecord principal)
            {
                writer.Field(principal.Kind.ToString());
                writer.Space();
                writer.Field(principal.Subject);
                writer.Space();
                writer.Field(principal.Since.ToString());
                writer.Space();
                writer.Field(principal.ProtectedUntil.ToString());
                writer.Raw(principal.Founder ? " 1" : " 0");
            }
            else
            {
                writer.Field(None);
                writer.Space();
                writer.Field(None);
                writer.Space();
                writer.Field(None);
                writer.Space();
                writer.Field(None);
                writer.Raw(" 0");
            }

            writer.End();
        }

        writer.Count("reputations", _reputation.Count);
        foreach (var (subject, tenths) in _reputation)
        {
            writer.TextNumber("reputation", subject, tenths);
        }

        writer.Count("changes", _changes.Count);
        foreach (var change in _changes)
        {
            writer.Begin("change");
            writer.Field(change.Subject);
            writer.Space();
            writer.Field(change.On.ToString());
            writer.Raw(" " + Number(change.DeltaTenths) + " " + Number(change.ResultTenths) + " ");
            writer.Field(change.ReasonKey);
            writer.End();
        }

        writer.Count("unemployed", _unemployed.Count);
        foreach (var record in _unemployed.Values)
        {
            writer.Begin("manager");
            writer.Field(record.Manager);
            writer.Space();
            writer.Field(record.Since.ToString());
            writer.Space();
            writer.Field(record.FormerOrganization?.Value ?? None);
            writer.Raw(" " + record.Severance.ToString(CultureInfo.InvariantCulture) + " " + Number(record.OffersMade) + " ");
            writer.Field(record.LastOfferOn?.ToString() ?? None);
            writer.End();
        }
    }

    private BoardSection WithReputation(string subject, int tenths, IReadOnlyList<ReputationChange> changes)
    {
        var map = new SortedDictionary<string, int>(_reputation, StringComparer.Ordinal) { [subject] = tenths };
        return new BoardSection(_boards, map, changes, _unemployed);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static SortedDictionary<string, T> NewMap<T>() => new(StringComparer.Ordinal);
}
