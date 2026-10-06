using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// The rules of opening, offering, signing and ending contracts, shared by the commands, the inbox resolvers and the day
/// handlers. Every public method is either a validation (returns the reason it cannot be done, or null) or a change that the
/// caller has just validated. A change only happens inside a command or a day tick (INV-001); a validation changes nothing and
/// draws no RNG (INV-005).
/// </summary>
public sealed class ContractEngine
{
    /// <summary>Inbox kind of a decision about a person's answer.</summary>
    public const string ResponseKind = "negotiation.response";

    /// <summary>Inbox kind of an information item about a negotiation that ended.</summary>
    public const string NoticeKind = "negotiation.notice";

    /// <summary>Inbox kind of the renewal prompt.</summary>
    public const string RenewalKind = "contract.renewal";

    /// <summary>Inbox kind of an information item about a contract.</summary>
    public const string ContractNoticeKind = "contract.notice";

    /// <summary>Argument name that ties an inbox item to its negotiation.</summary>
    public const string NegotiationArgument = "negotiation";

    /// <summary>Argument name that ties an inbox item to its contract.</summary>
    public const string ContractArgument = "contract";

    public const string OptionAccept = "accept";

    public const string OptionSign = "sign";

    public const string OptionWalk = "walk";

    public const string OptionRevise = "revise";

    public const string OptionRenew = "renew";

    public const string OptionRelease = "release";

    /// <summary>Extend on the current terms without a negotiation: the default of every renewal prompt.</summary>
    public const string OptionExtend = "extend";

    /// <summary>Inbox kind of the grouped renewal of staff who are not key staff.</summary>
    public const string RenewalGroupKind = "contract.renewalGroup";

    /// <summary>Argument name that lists the contracts of a grouped renewal, comma separated.</summary>
    public const string ContractsArgument = "contracts";

    /// <summary>ESTIMATE: seasons of the renewal offered from the renewal prompt.</summary>
    public const int DefaultRenewalYears = 2;

    private readonly ContractBook _book;
    private readonly InboxBook _inbox;
    private readonly ManagerRegistry _managers;

    public ContractEngine(ContractBook book, InboxBook inbox, ManagerRegistry managers)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(managers);
        _book = book;
        _inbox = inbox;
        _managers = managers;
    }

    public ContractBook Book => _book;

    /// <summary>The engine for the contract book, inbox and managers of a command context.</summary>
    public static ContractEngine From(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ContractEngine(context.Contracts, context.Inbox, context.Managers);
    }

    // ---------------------------------------------------------------- validation

    /// <summary>The negotiation, only when it belongs to <paramref name="manager"/>. Another manager's negotiation reads as missing (INV-003).</summary>
    public Negotiation? Own(ManagerId manager, string negotiationId)
    {
        ArgumentNullException.ThrowIfNull(negotiationId);
        var negotiation = _book.Section.Find(negotiationId);
        return negotiation is not null && negotiation.ManagerId == manager.Value ? negotiation : null;
    }

    public TranslationMessage? ValidateOpen(
        ManagerId manager,
        OrganizationId organization,
        PersonId person,
        NegotiationSubject subject,
        GameDate today,
        DateOnly? deadline,
        ContractId? renewalOf)
    {
        if (!_book.Environment.Control.Controls(manager, organization))
        {
            return TranslationMessage.Of(ContractKeys.NotController);
        }

        var record = FindPerson(person);
        if (record is null)
        {
            return TranslationMessage.Of(ContractKeys.UnknownPerson);
        }

        if (!subject.IsHeldBy(record.Roles))
        {
            return TranslationMessage.Of(ContractKeys.NotThatRole);
        }

        if (record.IsRetired)
        {
            return TranslationMessage.Of(ContractKeys.PersonRetired);
        }

        if (renewalOf is null)
        {
            var employment = CheckEmployment(organization, person, today);
            if (employment is not null)
            {
                return employment;
            }
        }

        var section = _book.Section;
        if (section.ActiveOf(organization).Any(negotiation => negotiation.Counterparty == person))
        {
            return TranslationMessage.Of(ContractKeys.AlreadyNegotiating);
        }

        var limit = _book.Capacity(organization);
        if (section.ActiveOf(organization).Count >= limit)
        {
            return TranslationMessage.Of(ContractKeys.TooManyOpen, ("limit", limit.ToString(CultureInfo.InvariantCulture)));
        }

        if (deadline is DateOnly named)
        {
            var days = today.DaysUntil(InboxBook.ToGameDate(named));
            if (days < NegotiationEstimates.MinDeadlineDays || days > NegotiationEstimates.MaxDeadlineDays)
            {
                return TranslationMessage.Of(
                    ContractKeys.BadDeadline,
                    ("min", NegotiationEstimates.MinDeadlineDays.ToString(CultureInfo.InvariantCulture)),
                    ("max", NegotiationEstimates.MaxDeadlineDays.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return null;
    }

    public TranslationMessage? ValidateSubmit(ManagerId manager, string negotiationId, OfferTerms terms, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var negotiation = Own(manager, negotiationId);
        if (negotiation is null)
        {
            return TranslationMessage.Of(ContractKeys.UnknownNegotiation);
        }

        if (!_book.Environment.Control.Controls(manager, negotiation.Proposer))
        {
            return TranslationMessage.Of(ContractKeys.NotController);
        }

        if (negotiation.Status is not (NegotiationStatus.Open or NegotiationStatus.Countered or NegotiationStatus.Considering))
        {
            return TranslationMessage.Of(ContractKeys.WrongStatus, ("status", negotiation.Status.ToString()));
        }

        if (today > negotiation.Deadline)
        {
            return TranslationMessage.Of(ContractKeys.DeadlinePassed);
        }

        if (negotiation.RoundsLeft <= 0)
        {
            return TranslationMessage.Of(ContractKeys.NoRoundsLeft);
        }

        if (!terms.Fits(negotiation.Subject))
        {
            return TranslationMessage.Of(ContractKeys.TermsDontFit);
        }

        if (FindPerson(negotiation.Counterparty)?.IsRetired != false)
        {
            return TranslationMessage.Of(ContractKeys.PersonRetired);
        }

        if (HasSignedFuture(negotiation.Counterparty, negotiation.RenewalOf, today))
        {
            return TranslationMessage.Of(ContractKeys.PersonTaken);
        }

        if (!_book.Environment.Payroll.CanCommit(negotiation.Proposer, terms.Salary, terms.Years, today))
        {
            return TranslationMessage.Of(ContractKeys.CannotAfford);
        }

        return null;
    }

    public TranslationMessage? ValidateAccept(ManagerId manager, string negotiationId, GameDate today)
    {
        var negotiation = Own(manager, negotiationId);
        if (negotiation is null)
        {
            return TranslationMessage.Of(ContractKeys.UnknownNegotiation);
        }

        if (!_book.Environment.Control.Controls(manager, negotiation.Proposer))
        {
            return TranslationMessage.Of(ContractKeys.NotController);
        }

        return SigningProblem(negotiation, today);
    }

    /// <summary>Why a negotiation cannot be signed today, or null. The manager is not checked here.</summary>
    public TranslationMessage? SigningProblem(Negotiation negotiation, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        if (negotiation.Status is not (NegotiationStatus.Countered or NegotiationStatus.PersonAgreed))
        {
            return TranslationMessage.Of(ContractKeys.WrongStatus, ("status", negotiation.Status.ToString()));
        }

        if (today > negotiation.Deadline)
        {
            return TranslationMessage.Of(ContractKeys.DeadlinePassed);
        }

        var person = negotiation.Counterparty;
        if (FindPerson(person)?.IsRetired != false)
        {
            return TranslationMessage.Of(ContractKeys.PersonRetired);
        }

        var terms = negotiation.Counter!;
        if (HasSignedFuture(person, negotiation.RenewalOf, today)
            || (negotiation.Status == NegotiationStatus.Countered && HoldsAnotherOrganization(negotiation)))
        {
            return TranslationMessage.Of(ContractKeys.PersonTaken);
        }

        if (negotiation.RenewalOf is null)
        {
            var employment = CheckEmployment(negotiation.Proposer, person, today, allowOwn: true);
            if (employment is not null)
            {
                return employment;
            }
        }

        if (!_book.Environment.Payroll.CanCommit(negotiation.Proposer, terms.Salary, terms.Years, today))
        {
            return TranslationMessage.Of(ContractKeys.CannotAfford);
        }

        return null;
    }

    public TranslationMessage? ValidateWalk(ManagerId manager, string negotiationId)
    {
        var negotiation = Own(manager, negotiationId);
        if (negotiation is null)
        {
            return TranslationMessage.Of(ContractKeys.UnknownNegotiation);
        }

        if (!_book.Environment.Control.Controls(manager, negotiation.Proposer))
        {
            return TranslationMessage.Of(ContractKeys.NotController);
        }

        return negotiation.IsActive
            ? null
            : TranslationMessage.Of(ContractKeys.WrongStatus, ("status", negotiation.Status.ToString()));
    }

    /// <summary>Why a contract cannot be renewed, or null. A renewal either exercises the team's option or opens a negotiation.</summary>
    public TranslationMessage? ValidateRenew(
        ManagerId manager,
        ContractId contractId,
        bool exerciseOption,
        OfferTerms? offer,
        DateOnly? deadline,
        GameDate today)
    {
        var contract = FindContract(contractId);
        if (contract is null || !_book.Environment.Control.Controls(manager, contract.OrganizationId))
        {
            return TranslationMessage.Of(ContractKeys.NotEmployer);
        }

        if (!contract.IsActiveOn(today))
        {
            return TranslationMessage.Of(ContractKeys.ContractNotActive);
        }

        if (exerciseOption)
        {
            var terms = _book.Section.TermsOf(contract.Id);
            if (contract.Option is not ContractOption option
                || terms?.OptionHolder != OptionHolder.Team
                || today > option.Deadline)
            {
                return TranslationMessage.Of(ContractKeys.OptionUnavailable);
            }

            return HasSignedFuture(contract.PersonId, contract.Id, today)
                ? TranslationMessage.Of(ContractKeys.PersonTaken)
                : null;
        }

        if (offer is null)
        {
            return TranslationMessage.Of(ContractKeys.OfferRequired);
        }

        if (today.DaysUntil(contract.End) > NegotiationEstimates.NegotiationWindowDays)
        {
            return TranslationMessage.Of(ContractKeys.TooEarlyToRenew);
        }

        var subject = SubjectOf(contract);
        if (!offer.Fits(subject))
        {
            return TranslationMessage.Of(ContractKeys.TermsDontFit);
        }

        if (HasSignedFuture(contract.PersonId, contract.Id, today))
        {
            return TranslationMessage.Of(ContractKeys.PersonTaken);
        }

        var opening = ValidateOpen(manager, contract.OrganizationId, contract.PersonId, subject, today, deadline, contract.Id);
        if (opening is not null)
        {
            return opening;
        }

        return _book.Environment.Payroll.CanCommit(contract.OrganizationId, offer.Salary, offer.Years, today)
            ? null
            : TranslationMessage.Of(ContractKeys.CannotAfford);
    }

    /// <summary>The least compensation an employer owes for ending a contract today (ESTIMATE: half the remaining salary).</summary>
    public static long MinimumCompensation(Contract contract, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var days = Math.Max(0, today.DaysUntil(contract.End));
        var remaining = contract.Salary * (days / 365.0);
        return (long)Math.Ceiling(remaining * NegotiationEstimates.TerminationShare);
    }

    public TranslationMessage? ValidateTerminate(ManagerId manager, ContractId contractId, long compensation, GameDate today)
    {
        var contract = FindContract(contractId);
        if (contract is null || !_book.Environment.Control.Controls(manager, contract.OrganizationId))
        {
            return TranslationMessage.Of(ContractKeys.NotEmployer);
        }

        if (!contract.IsActiveOn(today))
        {
            return TranslationMessage.Of(ContractKeys.ContractNotActive);
        }

        var minimum = MinimumCompensation(contract, today);
        if (compensation < minimum)
        {
            return TranslationMessage.Of(ContractKeys.CompensationTooLow, ("minimum", minimum.ToString(CultureInfo.InvariantCulture)));
        }

        return _book.Environment.Payroll.CanPay(contract.OrganizationId, compensation, today)
            ? null
            : TranslationMessage.Of(ContractKeys.CannotPay);
    }

    // ---------------------------------------------------------------- changes

    public (Negotiation Negotiation, IReadOnlyList<IDomainEvent> Events) Open(
        ManagerId manager,
        OrganizationId organization,
        PersonId person,
        NegotiationSubject subject,
        GameDate today,
        DateOnly? deadline,
        ContractId? renewalOf)
    {
        var last = deadline is DateOnly named
            ? InboxBook.ToGameDate(named)
            : today.AddDays(NegotiationEstimates.DefaultDeadlineDays);
        var rounds = NegotiationCore.MaxRounds(_book.Environment.Personality.TraitsOf(person));
        var (section, negotiation) = _book.Section.Open(manager.Value, organization, person, subject, renewalOf, today, last, rounds);
        _book.Update(section);
        return (negotiation, [new NegotiationOpened(manager, InboxBook.ToDateOnly(today), negotiation.Id, person.Value)]);
    }

    public IReadOnlyList<IDomainEvent> Submit(ManagerId manager, string negotiationId, OfferTerms terms, GameDate today)
    {
        var negotiation = _book.Section.Find(negotiationId) ?? throw new InvalidOperationException("Submit ran for a command that should have been rejected.");
        var nudge = negotiation.WouldBeNudge(terms);
        var changed = negotiation.WithOffer(terms, today);
        _book.Update(_book.Section.Replace(changed));
        CloseResponseItems(negotiation.Id, OptionRevise, today);
        return [new OfferSubmitted(manager, InboxBook.ToDateOnly(today), negotiation.Id, changed.RoundsUsed, nudge)];
    }

    public IReadOnlyList<IDomainEvent> WalkAway(ManagerId manager, string negotiationId, GameDate today, string? answeredItemId = null)
    {
        var negotiation = _book.Section.Find(negotiationId) ?? throw new InvalidOperationException("Walk ran for a command that should have been rejected.");
        var changed = negotiation.WithWalkAway(today);
        _book.Update(_book.Section.Replace(changed));
        CloseResponseItems(negotiation.Id, OptionWalk, today, answeredItemId);
        return [new NegotiationEnded(manager, InboxBook.ToDateOnly(today), negotiation.Id, NegotiationStatus.WalkedAway)];
    }

    /// <summary>
    /// Signs the terms the person holds to. The contract starts the day after the one the person holds ends (a renewal or a
    /// move at the end of a contract) or today. Every other live negotiation about the person is lost. The caller has validated.
    /// </summary>
    public IReadOnlyList<IDomainEvent> Sign(Negotiation negotiation, GameDate today, string? answeredItemId = null)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        var terms = negotiation.Counter ?? throw new InvalidOperationException("There are no terms to sign.");
        var person = negotiation.Counterparty;
        var latest = _book.LiveContractsOf(person, today).Select(contract => (GameDate?)contract.End).LastOrDefault();
        var start = latest is GameDate end ? end.AddDays(1) : today;
        var finish = NegotiationEstimates.EndFor(start, terms.Years);
        var option = ContractTerms.OptionFor(terms.Option, start, finish);
        var spec = new ContractSpec(
            person,
            negotiation.Proposer,
            negotiation.Subject.ToContractRole(terms),
            start,
            finish,
            terms.Salary,
            true,
            option,
            null);
        var (world, contractId) = _book.World.AddContract(spec);
        var section = _book.Section
            .WithTerms(new ContractTerms(contractId, terms.PointsBonus, terms.WinBonus, terms.TitleBonus, terms.Option?.Holder, terms.Exit))
            .Replace(negotiation.WithSigning(contractId, today));
        _book.Update(world, section);
        CloseResponseItems(negotiation.Id, negotiation.Status == NegotiationStatus.Countered ? OptionAccept : OptionSign, today, answeredItemId);

        var events = new List<IDomainEvent>
        {
            new ContractSigned(new ManagerId(negotiation.ManagerId), InboxBook.ToDateOnly(today), negotiation.Id, contractId.Value),
        };
        foreach (var other in _book.Section.ActiveFor(person))
        {
            if (other.Id == negotiation.Id)
            {
                continue;
            }

            events.Add(Lose(other, [NegotiationReasons.SignedElsewhere], today));
        }

        return events;
    }

    /// <summary>A negotiation is lost (the person went elsewhere). The manager is told with an information item.</summary>
    public IDomainEvent Lose(Negotiation negotiation, IReadOnlyList<string> reasons, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        var changed = negotiation.WithLoss(reasons, today);
        _book.Update(_book.Section.Replace(changed));
        CloseResponseItems(negotiation.Id, OptionWalk, today);
        PostNotice(changed, ContractKeys.InboxLostSubject, today);
        return new NegotiationEnded(new ManagerId(negotiation.ManagerId), InboxBook.ToDateOnly(today), negotiation.Id, NegotiationStatus.Lost);
    }

    public IReadOnlyList<IDomainEvent> RenewByOption(ManagerId manager, ContractId contractId, GameDate today)
    {
        var contract = FindContract(contractId) ?? throw new InvalidOperationException("Renew ran for a command that should have been rejected.");
        var extension = contract.Option!.Value.ExtraYears;
        var newEnd = GameDate.SeasonEnd(contract.End.Year + extension);
        var world = _book.World.WithContractTerm(contract.Id, newEnd, null);
        var terms = _book.Section.TermsOf(contract.Id);
        var section = _book.Section.WithTerms(new ContractTerms(
            contract.Id,
            terms?.PointsBonus ?? 0,
            terms?.WinBonus ?? 0,
            terms?.TitleBonus ?? 0,
            null,
            terms?.Exit));
        _book.Update(world, section);
        return [new OptionExercised(manager, InboxBook.ToDateOnly(today), contract.Id.Value, InboxBook.ToDateOnly(newEnd))];
    }

    /// <summary>Opens a renewal negotiation for a contract and makes the first offer in one step.</summary>
    public IReadOnlyList<IDomainEvent> OpenRenewal(ManagerId manager, ContractId contractId, OfferTerms offer, DateOnly? deadline, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var contract = FindContract(contractId) ?? throw new InvalidOperationException("Renew ran for a command that should have been rejected.");
        var (negotiation, opened) = Open(manager, contract.OrganizationId, contract.PersonId, SubjectOf(contract), today, deadline, contract.Id);
        var events = new List<IDomainEvent>(opened);
        events.AddRange(Submit(manager, negotiation.Id, offer, today));
        return events;
    }

    /// <summary>
    /// The first offer of a renewal made from the renewal prompt: the terms of the current contract (a salary of zero reads as the
    /// reference) for <see cref="DefaultRenewalYears"/> seasons.
    /// </summary>
    public OfferTerms DefaultRenewalOffer(Contract contract, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var reference = _book.ReferenceSalary(contract.OrganizationId, contract.PersonId, SubjectOf(contract), today);
        var current = _book.AsTerms(contract, reference, today);
        return new OfferTerms(
            current.Salary,
            current.PointsBonus,
            current.WinBonus,
            current.TitleBonus,
            DefaultRenewalYears,
            current.Seat,
            current.Option,
            current.Exit);
    }

    /// <summary>
    /// Why a contract cannot be extended on its current terms without a negotiation, or null. The employer must run it, it must
    /// still be live and not renewed yet, and (unless <paramref name="boardDecision"/>, the board's matter for a principal) the
    /// budget must carry it and the person must find staying worth at least what a new offer would have to be.
    /// </summary>
    public TranslationMessage? ValidateExtend(ManagerId manager, ContractId contractId, GameDate today, bool boardDecision = false)
    {
        var contract = FindContract(contractId);
        if (contract is null || !_book.Environment.Control.Controls(manager, contract.OrganizationId))
        {
            return TranslationMessage.Of(ContractKeys.NotEmployer);
        }

        if (!contract.IsActiveOn(today))
        {
            return TranslationMessage.Of(ContractKeys.ContractNotActive);
        }

        if (HasSignedFuture(contract.PersonId, contract.Id, today))
        {
            return TranslationMessage.Of(ContractKeys.PersonTaken);
        }

        if (boardDecision)
        {
            return null;
        }

        if (!_book.Environment.Payroll.CanCommit(contract.OrganizationId, contract.Salary, DefaultRenewalYears, today))
        {
            return TranslationMessage.Of(ContractKeys.CannotAfford);
        }

        return WouldStay(contract, today) ? null : TranslationMessage.Of(ContractKeys.WantsMore);
    }

    /// <summary>
    /// Extends a contract by <see cref="DefaultRenewalYears"/> seasons on its current terms: a new contract that starts the day
    /// after this one ends. No negotiation, so the negotiation cap does not apply and nothing waits for an answer. Draws no RNG.
    /// The caller has validated.
    /// </summary>
    public IReadOnlyList<IDomainEvent> Extend(ManagerId manager, ContractId contractId, GameDate today)
    {
        var contract = FindContract(contractId) ?? throw new InvalidOperationException("Extend ran for a request that should have been rejected.");
        var start = contract.End.AddDays(1);
        var finish = NegotiationEstimates.EndFor(start, DefaultRenewalYears);
        var spec = new ContractSpec(contract.PersonId, contract.OrganizationId, contract.Role, start, finish, contract.Salary, true, null, null);
        var (world, newId) = _book.World.AddContract(spec);
        var terms = _book.Section.TermsOf(contract.Id);
        var section = _book.Section.WithTerms(new ContractTerms(
            newId,
            terms?.PointsBonus ?? 0,
            terms?.WinBonus ?? 0,
            terms?.TitleBonus ?? 0,
            null,
            terms?.Exit));
        _book.Update(world, section);
        return [new ContractExtended(manager, InboxBook.ToDateOnly(today), contract.Id.Value, newId.Value)];
    }

    /// <summary>True when the person finds staying worth at least what a new offer would have to be (the test of a person's option).</summary>
    public bool WouldStay(Contract contract, GameDate today)
    {
        var stay = _book.UtilityOfStaying(contract, today);
        var subject = SubjectOf(contract);
        var reference = _book.ReferenceSalary(contract.OrganizationId, contract.PersonId, subject, today);
        var evaluation = _book.ContextFor(contract.OrganizationId, contract.PersonId, subject, today, reference);
        var needed = CounterpartyEvaluator.Threshold(CounterpartyEvaluator.Floor(evaluation.Age, null), NegotiationEstimates.InterestStart);
        return stay >= needed;
    }

    public IReadOnlyList<IDomainEvent> Terminate(ManagerId manager, ContractId contractId, long compensation, GameDate today)
    {
        var contract = FindContract(contractId) ?? throw new InvalidOperationException("Terminate ran for a command that should have been rejected.");
        _book.Environment.Payroll.RecordCompensation(contract.OrganizationId, contract.PersonId, compensation, today);
        _book.Update(_book.World.WithContractTerm(contract.Id, today, null));
        var events = new List<IDomainEvent> { new ContractTerminated(manager, InboxBook.ToDateOnly(today), contract.Id.Value, compensation) };
        foreach (var negotiation in _book.Section.ActiveFor(contract.PersonId))
        {
            if (negotiation.RenewalOf == contract.Id)
            {
                var ended = negotiation.WithWalkAway(today);
                _book.Update(_book.Section.Replace(ended));
                CloseResponseItems(negotiation.Id, OptionWalk, today);
                events.Add(new NegotiationEnded(new ManagerId(negotiation.ManagerId), InboxBook.ToDateOnly(today), negotiation.Id, NegotiationStatus.WalkedAway));
            }
        }

        return events;
    }

    // ---------------------------------------------------------------- inbox

    /// <summary>
    /// Tells a human manager about a person's answer: a decision for a counter or an agreement, nothing for the rest here.
    /// An AI manager reads the negotiation itself, so nothing is posted.
    /// </summary>
    public void PostResponse(Negotiation negotiation, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        if (!IsHuman(negotiation.ManagerId))
        {
            return;
        }

        var agreed = negotiation.Status == NegotiationStatus.PersonAgreed;
        var terms = negotiation.Counter ?? throw new InvalidOperationException("A response item needs the terms the person holds to.");
        var options = agreed
            ? new[]
            {
                new InboxOption(OptionSign, ContractKeys.InboxSignLabel, ContractKeys.InboxSignConsequence),
                new InboxOption(OptionWalk, ContractKeys.InboxWalkLabel, ContractKeys.InboxWalkConsequence),
            }
            : new[]
            {
                new InboxOption(OptionAccept, ContractKeys.InboxAcceptLabel, ContractKeys.InboxAcceptConsequence),
                new InboxOption(OptionWalk, ContractKeys.InboxWalkLabel, ContractKeys.InboxWalkConsequence),
                new InboxOption(OptionRevise, ContractKeys.InboxReviseLabel, ContractKeys.InboxReviseConsequence),
            };
        var draft = new InboxItemDraft(
            ResponseKind,
            agreed ? ContractKeys.InboxAgreedSubject : ContractKeys.InboxCounterSubject,
            ArgumentsOf(negotiation, terms),
            options,
            null,
            null);
        _inbox.Post(_managers, new ManagerId(negotiation.ManagerId), draft, today);
    }

    /// <summary>Tells a human manager that a negotiation ended without them acting (a refusal, a loss, a lapse).</summary>
    public void PostNotice(Negotiation negotiation, string subjectKey, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        if (!IsHuman(negotiation.ManagerId))
        {
            return;
        }

        var draft = new InboxItemDraft(
            NoticeKind,
            subjectKey,
            ArgumentsOf(negotiation, negotiation.CurrentOffer),
            null,
            null,
            null);
        _inbox.Post(_managers, new ManagerId(negotiation.ManagerId), draft, today);
    }

    /// <summary>Posts the renewal prompt to one human manager. The caller builds the draft.</summary>
    public void PostRenewalPrompt(ManagerId manager, InboxItemDraft draft, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _inbox.Post(_managers, manager, draft, today);
    }

    /// <summary>Tells a human manager something about one contract of theirs (an exit clause used, an option used, an expiry).</summary>
    public void PostContractNotice(ManagerId manager, Contract contract, string subjectKey, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!IsHuman(manager.Value))
        {
            return;
        }

        var draft = new InboxItemDraft(
            ContractNoticeKind,
            subjectKey,
            [
                new(ContractArgument, contract.Id.Value),
                new("person", _book.World.GetPerson(contract.PersonId).Name),
                new("end", contract.End.ToString()),
            ],
            null,
            null,
            null);
        _inbox.Post(_managers, manager, draft, today);
    }

    /// <summary>
    /// Closes every open decision item of a negotiation by recording <paramref name="optionId"/> as the answer, without running
    /// a resolver: the change that made the item moot has already happened (a new offer, a walk away, a signing).
    /// </summary>
    public void CloseResponseItems(string negotiationId, string optionId, GameDate today, string? skipItemId = null)
    {
        foreach (var item in _inbox.Section.Items)
        {
            if (!item.IsOpenDecision
                || item.Id == skipItemId
                || item.Kind != ResponseKind
                || !item.Arguments.TryGetValue(NegotiationArgument, out var id)
                || id != negotiationId)
            {
                continue;
            }

            var chosen = item.Options.Any(option => option.Id == optionId)
                ? optionId
                : item.Options.Any(option => option.Id == OptionAccept) && optionId == OptionSign ? OptionAccept : OptionWalk;
            if (item.Options.Any(option => option.Id == chosen))
            {
                _inbox.Resolve(_managers, item.Id, chosen, today);
            }
        }
    }

    /// <summary>The kinds of an organization's managers that are human, in ordinal order of their ids.</summary>
    public IReadOnlyList<ManagerId> HumanManagersOf(OrganizationId organization) =>
        _book.Environment.Control.ManagersOf(organization).Where(manager => IsHuman(manager.Value)).ToArray();

    public bool IsHuman(string managerId)
    {
        var id = new ManagerId(managerId);
        return _managers.Contains(id) && _managers.KindOf(id) == ManagerKind.Human;
    }

    // ---------------------------------------------------------------- helpers

    public Contract? FindContract(ContractId id)
    {
        if (!id.IsAssigned)
        {
            return null;
        }

        return _book.World.Contracts.FirstOrDefault(contract => contract.Id == id);
    }

    public Person? FindPerson(PersonId id)
    {
        if (!id.IsAssigned)
        {
            return null;
        }

        return _book.World.Persons.FirstOrDefault(person => person.Id == id);
    }

    public static NegotiationSubject SubjectOf(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return contract.Role.IsDriver ? NegotiationSubject.DriverSeat : NegotiationSubject.Staff(contract.Role.StaffRole);
    }

    private TranslationMessage? CheckEmployment(OrganizationId organization, PersonId person, GameDate today, bool allowOwn = false)
    {
        foreach (var contract in _book.LiveContractsOf(person, today))
        {
            if (contract.OrganizationId == organization)
            {
                if (!allowOwn)
                {
                    return TranslationMessage.Of(ContractKeys.OwnEmployee);
                }

                continue;
            }

            if (contract.Start > today)
            {
                return TranslationMessage.Of(ContractKeys.PersonTaken);
            }

            if (today.DaysUntil(contract.End) > NegotiationEstimates.NegotiationWindowDays && !ExitTriggered(contract, today))
            {
                return TranslationMessage.Of(ContractKeys.PersonUnderContract);
            }
        }

        return null;
    }

    private bool ExitTriggered(Contract contract, GameDate today)
    {
        var terms = _book.Section.TermsOf(contract.Id);
        return terms?.Exit is ExitClause exit
            && _book.Environment.Standings.Position(contract.OrganizationId, today.Season) is int position
            && exit.IsTriggeredBy(position);
    }

    /// <summary>True when the person already holds a contract that starts after today (other than the one being renewed).</summary>
    private bool HasSignedFuture(PersonId person, ContractId? renewalOf, GameDate today) =>
        _book.LiveContractsOf(person, today).Any(contract => contract.Start > today && contract.Id != renewalOf);

    /// <summary>True when the person has told another organization they would sign, so this organization has to wait.</summary>
    private bool HoldsAnotherOrganization(Negotiation negotiation) =>
        _book.Section.ActiveFor(negotiation.Counterparty).Any(other =>
            other.Id != negotiation.Id
            && other.Status == NegotiationStatus.PersonAgreed
            && other.Proposer != negotiation.Proposer);

    private IEnumerable<KeyValuePair<string, string>> ArgumentsOf(Negotiation negotiation, OfferTerms? terms)
    {
        var arguments = new List<KeyValuePair<string, string>>
        {
            new(NegotiationArgument, negotiation.Id),
            new("person", _book.World.GetPerson(negotiation.Counterparty).Name),
        };
        if (terms is not null)
        {
            arguments.Add(new("salary", terms.Salary.ToString(CultureInfo.InvariantCulture)));
            arguments.Add(new("years", terms.Years.ToString(CultureInfo.InvariantCulture)));
        }

        return arguments;
    }
}
