using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Pool;
using Paddock.Domain.Finance;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Finance;

/// <summary>
/// One posted line of the own ledger, as the principal reads it. <see cref="Category"/> is a ledger category id (the page
/// names it through a key); <see cref="AmountCents"/> is signed, income positive. The counterparty id stays out: it is
/// an internal id and the reason already says what the money was for.
/// </summary>
public sealed record LedgerLineView(DateOnly On, string Category, long AmountCents, TranslationMessage Reason);

/// <summary>
/// What a viewer may see (INV-003). The owning manager or AI sees cash, obligations, certain income and a forecast.
/// Anyone else's organization is <see cref="FinanceView.Unknown"/>. A developer sees the organization's own figures.
/// The read does not change state and does not draw a random number (INV-005).
/// </summary>
public abstract record FinanceView
{
    private FinanceView()
    {
    }

    public sealed record Own(
        long CashCents,
        long ObligationsCents,
        long CertainIncomeCents,
        long ForecastCashCents,
        int LoanOffers,
        TranslationMessage ForecastNote,
        IReadOnlyList<LedgerLineView> Ledger) : FinanceView;

    public sealed record Unknown(TranslationMessage Reason) : FinanceView;
}

public static class FinanceQuery
{
    /// <summary>
    /// How many of the latest ledger lines a read carries when it asks for them. The ledger itself keeps every line. Only the
    /// page asks: the daily reads of the AI need the cash and not a copy of the book.
    /// </summary>
    public const int LedgerLines = 60;

    public static FinanceView Read(
        AccessContext access,
        OrganizationId subject,
        WorldState world,
        GameDate today,
        IOrganizationControl control,
        ILoanFacility? loans = null,
        bool withLedger = false)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(control);
        loans ??= NoLoanFacility.Instance;
        var allowed = access.Kind == AccessKind.Developer
            || (access.Manager is { } manager && control.Controls(new Paddock.Application.Managers.ManagerId(manager.Value), subject));
        if (!allowed)
        {
            return new FinanceView.Unknown(TranslationMessage.Of(FinanceKeys.ViewUnknown));
        }

        var section = world.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;
        var outlook = section.Outlook(subject, world, today);
        var noteKey = outlook.WarningKey ?? FinanceKeys.ViewForecast;
        return new FinanceView.Own(
            outlook.CashCents,
            outlook.ObligationsCents,
            outlook.CertainIncomeCents,
            outlook.ForecastCashCents,
            loans.Offers(subject, today).Count,
            TranslationMessage.Of(noteKey),
            withLedger ? Latest(section.EntriesOf(subject)) : []);
    }

    /// <summary>The newest lines first. The ledger is kept in posting order, so the tail is the latest.</summary>
    private static LedgerLineView[] Latest(IReadOnlyList<LedgerEntry> entries)
    {
        var count = Math.Min(LedgerLines, entries.Count);
        var lines = new LedgerLineView[count];
        for (var at = 0; at < count; at++)
        {
            var entry = entries[entries.Count - 1 - at];
            lines[at] = new LedgerLineView(
                new DateOnly(entry.Date.Year, entry.Date.Month, entry.Date.Day),
                entry.Category,
                entry.AmountCents,
                TranslationMessage.Of(entry.ReasonKey));
        }

        return lines;
    }
}

/// <summary>
/// The ledger as the contract port <see cref="IPayrollLedger"/>. Overdraft is allowed until the organization is insolvent
/// (PP-050). Amounts on the port are whole nominal dollars, the unit contracts already use; the ledger stores cents.
/// </summary>
public sealed class FinancePayroll : IPayrollLedger
{
    private readonly FinanceBook _book;

    public FinancePayroll(FinanceBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public bool CanCommit(OrganizationId organization, long annualSalary, int years, GameDate on) => CanPay(organization, annualSalary, on);

    public bool CanPay(OrganizationId organization, long amount, GameDate on) =>
        _book.Section.HasBook(organization) && !_book.Section.IsInsolvent(organization);

    public void RecordCompensation(OrganizationId payer, PersonId payee, long amount, GameDate on)
    {
        if (amount <= 0 || !CanPay(payer, amount, on))
        {
            throw new InvalidOperationException("Compensation was recorded for an organization that cannot pay.");
        }

        _book.Replace(_book.Section.Post(
            payer,
            on,
            LedgerCategories.Other,
            payee.Value,
            -Money.FromDollars(amount).Cents,
            FinanceReason.Termination));
    }
}

/// <summary>
/// The ledger as <see cref="IJuniorFunding"/>. The price is a share of the season's typical team budget (#268: a fixed sum is nothing in 1976 and
/// everything in 1955); a ledger with no typical budget yet uses the nominal sum. The posting is cents.
/// An insolvent organization is refused. A solvent one may go negative (PP-050).
/// </summary>
public sealed class FinanceJuniorFunding : IJuniorFunding
{
    private readonly FinanceBook _book;

    public FinanceJuniorFunding(FinanceBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public long Cost(JuniorProgramme programme)
    {
        var typicalCents = _book.Section.TypicalCents;
        if (typicalCents <= 0)
        {
            return PoolEstimates.CostOf(programme);
        }

        return Math.Max(1L, (long)Math.Round(typicalCents / 100.0 * PoolEstimates.CostShare(programme), MidpointRounding.AwayFromZero));
    }

    public TranslationMessage? Validate(OrganizationId payer, long amount, GameDate on)
    {
        if (!_book.Section.HasBook(payer))
        {
            return TranslationMessage.Of(FinanceKeys.NoBooks);
        }

        return _book.Section.IsInsolvent(payer) ? TranslationMessage.Of(FinanceKeys.Insolvent) : null;
    }

    public void Charge(OrganizationId payer, long amount, string reasonKey, GameDate on)
    {
        if (Validate(payer, amount, on) is not null || amount <= 0)
        {
            throw new InvalidOperationException("A junior programme was charged for an organization that cannot pay.");
        }

        _book.Replace(_book.Section.Post(
            payer,
            on,
            LedgerCategories.Development,
            null,
            -Money.FromDollars(amount).Cents,
            reasonKey));
    }
}

/// <summary>Parses organization ids the way the world stores them (<c>org:n</c> or an authored id).</summary>
public static class FinanceIds
{
    private const string OrganizationPrefix = "org:";

    public static OrganizationId Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.StartsWith(OrganizationPrefix, StringComparison.Ordinal))
        {
            var tail = text.AsSpan(OrganizationPrefix.Length);
            if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
                || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
            {
                throw new FormatException("Organization id '" + text + "' is malformed.");
            }

            var generated = OrganizationId.Generated(sequence);
            if (!string.Equals(generated.Value, text, StringComparison.Ordinal))
            {
                throw new FormatException("Organization id '" + text + "' is malformed.");
            }

            return generated;
        }

        return OrganizationId.Real(text);
    }

    public static bool TryParse(string text, out OrganizationId organization)
    {
        try
        {
            organization = Parse(text);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            organization = default;
            return false;
        }
    }
}
