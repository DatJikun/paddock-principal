using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.People;
using Paddock.Domain.Principals;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using Paddock.Simulation.Career;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Principals;

/// <summary>
/// The live world as the principals' command sees it: the host owns the world, this reads it and puts the <c>principals</c> section back.
/// <see cref="ForSession"/> is the career session's.
/// </summary>
public sealed class PrincipalsBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public PrincipalsBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public PrincipalsSection Section => World.Section<PrincipalsSection>(PrincipalsSection.SectionName) ?? PrincipalsSection.Empty;

    public static PrincipalsBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new PrincipalsBook(() => session.World, session.StoreWorld);
    }

    internal void Replace(PrincipalsSection section) => _write(World.WithSection(section));
}

public static class PrincipalEventTypes
{
    public const string Reviewed = "principals.reviewed";
}

/// <summary>An AI principal looked at its team. The event carries the archetype only as text for the log; no manager reads it.</summary>
public sealed record PrincipalReviewed(ManagerId ManagerId, DateOnly OccurredOn, string OrganizationId) : IDomainEvent
{
    public string TypeId => PrincipalEventTypes.Reviewed;
}

/// <summary>
/// An AI principal records that it has reviewed its team: who it is (archetype, and the person it was assigned for), when it looks
/// again, and what it remembers between reviews. It is the principal's only change to the world besides the commands it shares with
/// the player, and like every command it names the manager (the AI manager of the team) and goes through the normal pipeline, so the
/// section is replayable from the command log (INV-001, INV-002).
/// </summary>
public sealed record RecordPrincipalReviewCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    /// <summary>One of <c>Contender</c>, <c>Builder</c>, <c>Opportunist</c>, <c>Survivor</c>.</summary>
    public required string Archetype { get; init; }

    /// <summary>The principal the archetype is for, or empty when the team has none.</summary>
    public string Person { get; init; } = string.Empty;

    public required DateOnly NextReview { get; init; }

    public int SacrificedSeason { get; init; }

    public int ScoutSeason { get; init; }

    /// <summary>Key staff roles the principal considers part of the team, sorted and joined by commas.</summary>
    public string StaffRoles { get; init; } = string.Empty;

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

internal static class PrincipalIds
{
    public static bool TryPerson(string text, out PersonId person)
    {
        person = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            if (text.StartsWith("gen:", StringComparison.Ordinal))
            {
                var tail = text.AsSpan(4);
                if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
                    || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
                {
                    return false;
                }

                person = PersonId.Generated(sequence);
            }
            else
            {
                person = PersonId.Real(text);
            }

            return person.Value == text;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool ValidRoles(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var previous = string.Empty;
        foreach (var part in text.Split(','))
        {
            if (!Enum.TryParse<StaffRole>(part, ignoreCase: false, out var role) || !Enum.IsDefined(role) || role.ToString() != part
                || string.CompareOrdinal(previous, part) >= 0)
            {
                return false;
            }

            previous = part;
        }

        return true;
    }
}

public sealed class RecordPrincipalReviewHandler : CommandHandler<RecordPrincipalReviewCommand>
{
    private readonly PrincipalsBook _book;
    private readonly IOrganizationControl _control;

    public RecordPrincipalReviewHandler(PrincipalsBook book, IOrganizationControl control)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(control);
        _book = book;
        _control = control;
    }

    protected override TranslationMessage? ValidateTyped(RecordPrincipalReviewCommand command, CommandContext context)
    {
        var managers = context.Managers;
        if (!managers.Contains(command.ManagerId) || managers.KindOf(command.ManagerId) != ManagerKind.Ai)
        {
            return TranslationMessage.Of(PrincipalKeys.NotAnAiManager);
        }

        if (!FinanceIds.TryParse(command.OrganizationId, out var organization))
        {
            return TranslationMessage.Of(PrincipalKeys.UnknownOrganization);
        }

        var found = _book.World.Organizations.FirstOrDefault(candidate => candidate.Id == organization);
        if (found is null)
        {
            return TranslationMessage.Of(PrincipalKeys.UnknownOrganization);
        }

        if (found.Kind != OrganizationKind.Team)
        {
            return TranslationMessage.Of(PrincipalKeys.NotATeam);
        }

        if (!_control.Controls(command.ManagerId, organization))
        {
            return TranslationMessage.Of(PrincipalKeys.NotInControl);
        }

        // An AI never acts for an organization a human runs (ManagerRegistry says who is human).
        foreach (var other in _control.ManagersOf(organization))
        {
            if (managers.Contains(other) && managers.KindOf(other) == ManagerKind.Human)
            {
                return TranslationMessage.Of(PrincipalKeys.HumanTeam);
            }
        }

        if (!PrincipalArchetypes.TryParse(command.Archetype, out _))
        {
            return TranslationMessage.Of(PrincipalKeys.BadArchetype);
        }

        if (command.Person.Length != 0 && !PrincipalIds.TryPerson(command.Person, out _))
        {
            return TranslationMessage.Of(PrincipalKeys.BadPerson);
        }

        if (command.NextReview < command.IssuedOn)
        {
            return TranslationMessage.Of(PrincipalKeys.BadDate);
        }

        if (command.SacrificedSeason < 0 || command.ScoutSeason < 0)
        {
            return TranslationMessage.Of(PrincipalKeys.BadSeason);
        }

        return PrincipalIds.ValidRoles(command.StaffRoles) ? null : TranslationMessage.Of(PrincipalKeys.BadRoles);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RecordPrincipalReviewCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null || !FinanceIds.TryParse(command.OrganizationId, out var organization))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = new GameDate(command.IssuedOn.Year, command.IssuedOn.Month, command.IssuedOn.Day);
        PersonId? person = null;
        if (command.Person.Length != 0 && PrincipalIds.TryPerson(command.Person, out var parsed))
        {
            person = parsed;
        }

        var section = _book.Section;
        var before = section.Of(organization);
        var same = before is not null && before.Person == person && before.Archetype == command.Archetype;
        var record = new AiPrincipalRecord(
            organization,
            command.Archetype,
            person,
            same ? before!.AssignedOn : today,
            today,
            new GameDate(command.NextReview.Year, command.NextReview.Month, command.NextReview.Day),
            command.SacrificedSeason,
            command.ScoutSeason,
            command.StaffRoles);
        _book.Replace(section.With(record));
        return [new PrincipalReviewed(command.ManagerId, command.IssuedOn, organization.Value)];
    }
}

/// <summary>Saves and loads <see cref="RecordPrincipalReviewCommand"/> with the command log.</summary>
public static class PrincipalCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<RecordPrincipalReviewCommand>(
            "principals.review/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("archetype", command.Archetype),
                ("person", command.Person),
                ("nextReview", command.NextReview.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("sacrificed", command.SacrificedSeason),
                ("scouted", command.ScoutSeason),
                ("roles", command.StaffRoles)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "archetype", "person", "nextReview", "sacrificed", "scouted", "roles");
                return new RecordPrincipalReviewCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    Archetype = fields.String("archetype"),
                    Person = fields.String("person"),
                    NextReview = DateOnly.ParseExact(fields.String("nextReview"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SacrificedSeason = fields.Int32("sacrificed"),
                    ScoutSeason = fields.Int32("scouted"),
                    StaffRoles = fields.String("roles"),
                };
            }),
    ];
}
