using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.World;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Contracts;

/// <summary>
/// The save entries of the six contract commands (see <see cref="CommandCodec"/>). Ids and terms are written as text; an absent
/// value is a single dash. A command that changes shape gets a new tag version and keeps the old entry.
/// </summary>
public static class ContractCommandCodecs
{
    private const string None = "-";

    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<OpenNegotiationCommand>(
            "contract.openNegotiation/1",
            command => FlatJson.Write(
                ("organization", command.Organization.Value),
                ("person", command.Person.Value),
                ("subject", command.Subject.Key),
                ("deadline", command.Deadline is DateOnly deadline ? DateText(deadline) : None)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "person", "subject", "deadline");
                return new OpenNegotiationCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Organization = OrganizationIdOf(fields.String("organization")),
                    Person = PersonIdOf(fields.String("person")),
                    Subject = ParseSubject(fields.String("subject")),
                    Deadline = fields.String("deadline") == None ? null : ParseDate(fields.String("deadline")),
                };
            }),
        CommandCodecEntry.For<SubmitOfferCommand>(
            "contract.submitOffer/1",
            command => FlatJson.Write(("negotiation", command.NegotiationId), ("terms", TermsText(command.Terms))),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "negotiation", "terms");
                return new SubmitOfferCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    NegotiationId = fields.String("negotiation"),
                    Terms = ParseTerms(fields.String("terms")),
                };
            }),
        CommandCodecEntry.For<AcceptCounterOfferCommand>(
            "contract.acceptCounterOffer/1",
            command => FlatJson.Write(("negotiation", command.NegotiationId)),
            (body, manager, issued) => new AcceptCounterOfferCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                NegotiationId = FlatJson.Read(body, "negotiation").String("negotiation"),
            }),
        CommandCodecEntry.For<WalkAwayCommand>(
            "contract.walkAway/1",
            command => FlatJson.Write(("negotiation", command.NegotiationId)),
            (body, manager, issued) => new WalkAwayCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                NegotiationId = FlatJson.Read(body, "negotiation").String("negotiation"),
            }),
        CommandCodecEntry.For<RenewContractCommand>(
            "contract.renew/1",
            command => FlatJson.Write(
                ("contract", command.Contract.Value),
                ("exerciseOption", command.ExerciseOption ? "1" : "0"),
                ("offer", command.Offer is null ? None : TermsText(command.Offer)),
                ("deadline", command.Deadline is DateOnly deadline ? DateText(deadline) : None)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "contract", "exerciseOption", "offer", "deadline");
                return new RenewContractCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Contract = ContractIdOf(fields.String("contract")),
                    ExerciseOption = fields.String("exerciseOption") == "1",
                    Offer = fields.String("offer") == None ? null : ParseTerms(fields.String("offer")),
                    Deadline = fields.String("deadline") == None ? null : ParseDate(fields.String("deadline")),
                };
            }),
        CommandCodecEntry.For<TerminateContractCommand>(
            "contract.terminate/1",
            command => FlatJson.Write(("contract", command.Contract.Value), ("compensation", command.Compensation)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "contract", "compensation");
                return new TerminateContractCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    Contract = ContractIdOf(fields.String("contract")),
                    Compensation = fields.Int64("compensation"),
                };
            }),
    ];

    /// <summary>Terms as <c>salary,points,win,title,years,seat,option,exit</c>, with a dash for none.</summary>
    public static string TermsText(OfferTerms terms) => string.Join(
        ",",
        terms.Salary.ToString(CultureInfo.InvariantCulture),
        terms.PointsBonus.ToString(CultureInfo.InvariantCulture),
        terms.WinBonus.ToString(CultureInfo.InvariantCulture),
        terms.TitleBonus.ToString(CultureInfo.InvariantCulture),
        terms.Years.ToString(CultureInfo.InvariantCulture),
        terms.Seat is SeatStatus seat ? seat.ToString() : None,
        terms.Option is OfferOption option ? option.Holder + ":" + option.ExtraYears.ToString(CultureInfo.InvariantCulture) : None,
        terms.Exit is ExitClause exit ? exit.PositionWorseThan.ToString(CultureInfo.InvariantCulture) : None);

    public static OfferTerms ParseTerms(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 8)
        {
            throw new InvalidDataException("Offer terms need eight parts.");
        }

        try
        {
            OfferOption? option = null;
            if (parts[6] != None)
            {
                var pair = parts[6].Split(':');
                option = new OfferOption(Enum.Parse<OptionHolder>(pair[0]), int.Parse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture));
            }

            return new OfferTerms(
                long.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture),
                long.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture),
                long.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture),
                long.Parse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture),
                parts[5] == None ? null : Enum.Parse<SeatStatus>(parts[5]),
                option,
                parts[7] == None ? null : new ExitClause(int.Parse(parts[7], NumberStyles.None, CultureInfo.InvariantCulture)));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("Offer terms are malformed.", ex);
        }
    }

    private static string DateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new InvalidDataException("Date '" + text + "' is malformed.");

    private static NegotiationSubject ParseSubject(string text)
    {
        try
        {
            return NegotiationSubject.Parse(text);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static PersonId PersonIdOf(string text) =>
        text.StartsWith("gen:", StringComparison.Ordinal) ? PersonId.Generated(Sequence(text, "gen:")) : PersonId.Real(text);

    private static OrganizationId OrganizationIdOf(string text) =>
        text.StartsWith("org:", StringComparison.Ordinal) ? OrganizationId.Generated(Sequence(text, "org:")) : OrganizationId.Real(text);

    private static ContractId ContractIdOf(string text) => ContractId.Generated(Sequence(text, "con:"));

    private static long Sequence(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.Ordinal)
        && long.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
        && sequence >= 1
            ? sequence
            : throw new InvalidDataException("Id '" + text + "' is malformed.");
}
