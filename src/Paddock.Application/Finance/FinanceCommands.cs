using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Application.Finance;

public sealed record OpenBooksCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record ApplyRaceResultsCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required int Season { get; init; }

    public required int Round { get; init; }

    public required int RacesInSeason { get; init; }

    /// <summary><c>org|driver|position|classified</c> rows separated by <c>;</c>. Classified is <c>1</c> or <c>0</c>.</summary>
    public required string Entries { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record ApplySeasonEndedCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required int Season { get; init; }

    public required int Races { get; init; }

    /// <summary><c>org|position|points</c> rows separated by <c>;</c>. Points use the invariant decimal form.</summary>
    public required string Constructors { get; init; }

    /// <summary>Distinct winning organization ids, separated by commas. Empty when nobody won.</summary>
    public required string Winners { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed class OpenBooksHandler : CommandHandler<OpenBooksCommand>
{
    private readonly FinanceBook _book;
    private readonly IOrganizationControl _control;
    private readonly IEraFinanceSource _eras;
    private readonly ITeamTierSource _tiers;

    public OpenBooksHandler(FinanceBook book, IOrganizationControl control, IEraFinanceSource eras, ITeamTierSource tiers)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(eras);
        ArgumentNullException.ThrowIfNull(tiers);
        _book = book;
        _control = control;
        _eras = eras;
        _tiers = tiers;
    }

    protected override TranslationMessage? ValidateTyped(OpenBooksCommand command, CommandContext context)
    {
        if (!context.Managers.Contains(command.ManagerId))
        {
            return TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", command.ManagerId.Value));
        }

        if (!FinanceIds.TryParse(command.OrganizationId, out var organization))
        {
            return TranslationMessage.Of(FinanceKeys.UnknownOrganization);
        }

        if (!_control.Controls(command.ManagerId, organization))
        {
            return TranslationMessage.Of(FinanceKeys.NotYourOrganization);
        }

        if (!Owns(organization))
        {
            return TranslationMessage.Of(FinanceKeys.UnknownOrganization);
        }

        if (_book.Section.HasBook(organization))
        {
            return TranslationMessage.Of(FinanceKeys.BooksOpen);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(OpenBooksCommand command, CommandContext context)
    {
        var organization = FinanceIds.Parse(command.OrganizationId);
        var today = FinanceBook.ToGameDate(command.IssuedOn);
        var facts = _eras.Facts(today.Year);
        var dollars = facts.Dollars(_tiers.TierOf(organization, today.Year));
        _book.Replace(_book.Section.Open(organization, today, dollars, facts));
        return [new BooksOpened(command.ManagerId, command.IssuedOn, organization.Value, Money.FromDollars(dollars).Cents)];
    }

    private bool Owns(OrganizationId organization)
    {
        foreach (var candidate in _book.World.Organizations)
        {
            if (candidate.Id == organization)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class ApplyRaceResultsHandler : CommandHandler<ApplyRaceResultsCommand>
{
    private readonly FinanceBook _book;
    private readonly IEraFinanceSource _eras;

    public ApplyRaceResultsHandler(FinanceBook book, IEraFinanceSource eras)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(eras);
        _book = book;
        _eras = eras;
    }

    protected override TranslationMessage? ValidateTyped(ApplyRaceResultsCommand command, CommandContext context)
    {
        if (!context.Managers.Contains(command.ManagerId))
        {
            return TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", command.ManagerId.Value));
        }

        if (FinanceText.TryRace(command, _eras.Facts(command.Season).RevenueModel, out var race, out _) && race is not null)
        {
            foreach (var entry in race.Entries)
            {
                if (!_book.Section.HasBook(entry.Organization))
                {
                    return TranslationMessage.Of(FinanceKeys.NoBooks);
                }
            }

            return null;
        }

        return TranslationMessage.Of(FinanceKeys.Malformed);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ApplyRaceResultsCommand command, CommandContext context)
    {
        var facts = _eras.Facts(command.Season);
        if (!FinanceText.TryRace(command, facts.RevenueModel, out var race, out _) || race is null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var (section, _) = _book.Section.ApplyRace(race, Money.FromDollars(facts.TypicalDollars).Cents, FinanceBook.ToGameDate(command.IssuedOn));
        _book.Replace(section);
        return [new RaceMoneyPosted(command.ManagerId, command.IssuedOn, command.Season, command.Round)];
    }
}

public sealed class ApplySeasonEndedHandler : CommandHandler<ApplySeasonEndedCommand>
{
    private readonly FinanceBook _book;

    public ApplySeasonEndedHandler(FinanceBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    protected override TranslationMessage? ValidateTyped(ApplySeasonEndedCommand command, CommandContext context)
    {
        if (!context.Managers.Contains(command.ManagerId))
        {
            return TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", command.ManagerId.Value));
        }

        return FinanceText.TrySeason(command, out _, out _) ? null : TranslationMessage.Of(FinanceKeys.Malformed);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ApplySeasonEndedCommand command, CommandContext context)
    {
        if (!FinanceText.TrySeason(command, out var season, out _) || season is null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var section = _book.Section.ApplySeason(season);
        _book.Replace(section);
        return [new SeasonPopularityUpdated(command.ManagerId, command.IssuedOn, season.Season, section.PopularityMilli)];
    }
}

/// <summary>The compact text of race and season commands. Not player-facing.</summary>
public static class FinanceText
{
    public static string Race(IReadOnlyList<RaceEntryResult> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return string.Join(';', entries.Select(entry =>
            entry.Organization.Value + "|" + entry.DriverId + "|"
            + entry.Position.ToString(CultureInfo.InvariantCulture) + "|"
            + (entry.Classified ? "1" : "0")));
    }

    public static string Constructors(IReadOnlyList<ConstructorTitleRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return string.Join(';', rows.Select(row =>
            row.Organization.Value + "|"
            + row.Position.ToString(CultureInfo.InvariantCulture) + "|"
            + row.Points.ToString(CultureInfo.InvariantCulture)));
    }

    public static string Winners(IReadOnlyList<OrganizationId> winners) =>
        string.Join(',', winners.Select(winner => winner.Value));

    public static bool TryRace(ApplyRaceResultsCommand command, string revenueModel, out RaceResultsPublished? race, out string? error)
    {
        race = null;
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(command.Entries))
            {
                error = "empty";
                return false;
            }

            var entries = new List<RaceEntryResult>();
            foreach (var row in command.Entries.Split(';', StringSplitOptions.None))
            {
                var parts = row.Split('|');
                if (parts.Length != 4 || parts[3] is not ("0" or "1"))
                {
                    error = "row";
                    return false;
                }

                entries.Add(new RaceEntryResult(
                    FinanceIds.Parse(parts[0]),
                    parts[1],
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    parts[3] == "1"));
            }

            race = new RaceResultsPublished(command.Season, command.Round, command.RacesInSeason, revenueModel, entries);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            error = "parse";
            return false;
        }
    }

    public static bool TrySeason(ApplySeasonEndedCommand command, out SeasonEnded? season, out string? error)
    {
        season = null;
        error = null;
        try
        {
            var rows = new List<ConstructorTitleRow>();
            if (command.Constructors.Length > 0)
            {
                foreach (var row in command.Constructors.Split(';', StringSplitOptions.None))
                {
                    var parts = row.Split('|');
                    if (parts.Length != 3)
                    {
                        error = "row";
                        return false;
                    }

                    rows.Add(new ConstructorTitleRow(
                        FinanceIds.Parse(parts[0]),
                        int.Parse(parts[1], CultureInfo.InvariantCulture),
                        decimal.Parse(parts[2], CultureInfo.InvariantCulture)));
                }
            }

            var winners = new List<OrganizationId>();
            if (command.Winners.Length > 0)
            {
                foreach (var winner in command.Winners.Split(',', StringSplitOptions.None))
                {
                    winners.Add(FinanceIds.Parse(winner));
                }
            }

            season = new SeasonEnded(command.Season, command.Races, rows, winners);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            error = "parse";
            return false;
        }
    }
}
