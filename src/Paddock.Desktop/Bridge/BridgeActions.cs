using System.Globalization;
using System.Text.Json;
using Paddock.Application.Cars;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Pool;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Pool;
using Paddock.Domain.Supply;
using Paddock.Domain.World;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Desktop.Bridge;

/// <summary>Turns a page command into the Application command of the same name. It adds no rule.</summary>
internal static class BridgeActions
{
    public static bool TryBuild(string name, JsonElement args, CareerBridge career, out ICommand? command, out string? key)
    {
        command = null;
        key = null;
        var today = career.IssuedOn;
        var manager = career.Human;
        switch (name)
        {
            case "negotiateOpen":
                return OpenNegotiation(args, career, manager, today, out command, out key);
            case "negotiateOffer":
                return Offer(args, manager, today, out command, out key);
            case "negotiateAccept":
                return Id(args, "negotiationId", id => new AcceptCounterOfferCommand { ManagerId = manager, IssuedOn = today, NegotiationId = id }, out command, out key);
            case "negotiateWalk":
                return Id(args, "negotiationId", id => new WalkAwayCommand { ManagerId = manager, IssuedOn = today, NegotiationId = id }, out command, out key);
            case "negotiateRenew":
                return Renew(args, manager, today, out command, out key);
            case "sponsorBegin":
                return SponsorBegin(args, career, manager, today, out command, out key);
            case "sponsorSign":
                return SponsorTalk(args, career, manager, today, sign: true, out command, out key);
            case "sponsorWalk":
                return SponsorTalk(args, career, manager, today, sign: false, out command, out key);
            case "sponsorRespond":
                return SponsorRespond(args, career, manager, today, out command, out key);
            case "developmentSplit":
                return Split(args, career, manager, today, out command, out key);
            case "commitConcept":
                return Project(args, career, manager, today, commit: true, out command, out key);
            case "approveConcept":
                return Approve(args, career, manager, today, out command, out key);
            case "scoutFocus":
                command = new AssignScoutFocusCommand
                {
                    ManagerId = manager,
                    IssuedOn = today,
                    PersonHandle = BridgeText.Optional(args, "personHandle"),
                };
                return true;
            case "fundJunior":
                return Fund(args, manager, today, out command, out key);
            case "signPoolDriver":
                return SignPool(args, manager, today, out command, out key);
            case "supplyPropose":
                return Supply(args, career, manager, today, out command, out key);
            case "supplyRespond":
                return SupplyRespond(args, career, manager, today, out command, out key);
            default:
                return false;
        }
    }

    private static bool OpenNegotiation(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var personText = BridgeText.Required(args, "personId");
        var subjectText = BridgeText.Required(args, "subject");
        if (personText is null || subjectText is null || !BridgeIds.TryPerson(personText, out var person))
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        NegotiationSubject subject;
        try
        {
            subject = NegotiationSubject.Parse(subjectText);
        }
        catch (ArgumentException)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Organization(args, career, out var organization, out key))
        {
            return false;
        }

        command = new OpenNegotiationCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            Organization = organization,
            Person = person,
            Subject = subject,
        };
        return true;
    }

    private static bool Offer(JsonElement args, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var id = BridgeText.Required(args, "negotiationId");
        var salary = BridgeText.Long(args, "salary");
        var years = BridgeText.Int(args, "years");
        if (id is null || salary is null || years is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        command = new SubmitOfferCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            NegotiationId = id,
            Terms = new OfferTerms(salary.Value, 0, 0, 0, years.Value, SeatStatus.Equal, null, null),
        };
        key = null;
        return true;
    }

    private static bool Id(JsonElement args, string field, Func<string, ICommand> build, out ICommand? command, out string? key)
    {
        var value = BridgeText.Required(args, field);
        if (value is null)
        {
            command = null;
            key = BridgeKeys.BadMessage;
            return false;
        }

        command = build(value);
        key = null;
        return true;
    }

    private static bool Renew(JsonElement args, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var contractText = BridgeText.Required(args, "contractId");
        var exercise = BridgeText.Bool(args, "exerciseOption");
        if (contractText is null || exercise is null || !BridgeIds.TryContract(contractText, out var contract))
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        OfferTerms? terms = null;
        if (!exercise.Value)
        {
            var salary = BridgeText.Long(args, "salary");
            var years = BridgeText.Int(args, "years");
            if (salary is null || years is null)
            {
                key = BridgeKeys.BadMessage;
                return false;
            }

            terms = new OfferTerms(salary.Value, 0, 0, 0, years.Value, SeatStatus.Equal, null, null);
        }

        command = new RenewContractCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            Contract = contract,
            ExerciseOption = exercise.Value,
            Offer = terms,
        };
        key = null;
        return true;
    }

    private static bool SponsorBegin(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var sponsor = BridgeText.Required(args, "sponsorId");
        var slot = BridgeText.Int(args, "slot");
        if (sponsor is null || slot is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new BeginSponsorTalksCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            SponsorId = sponsor,
            Slot = slot.Value,
        };
        return true;
    }

    private static bool SponsorTalk(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, bool sign, out ICommand? command, out string? key)
    {
        command = null;
        var talk = BridgeText.Required(args, "talkId");
        if (talk is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = sign
            ? new SignAtCurrentTermsCommand { ManagerId = manager, IssuedOn = today, OrganizationId = organization, TalkId = talk }
            : new WalkAwayFromTalksCommand { ManagerId = manager, IssuedOn = today, OrganizationId = organization, TalkId = talk };
        return true;
    }

    private static bool SponsorRespond(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var offer = BridgeText.Required(args, "offerId");
        var accept = BridgeText.Bool(args, "accept");
        if (offer is null || accept is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new RespondToSponsorOfferCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            OfferId = offer,
            Accept = accept.Value,
        };
        return true;
    }

    private static bool Split(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var current = BridgeText.Int(args, "currentPercent");
        var account = BridgeText.Int(args, "accountPercent");
        var next = BridgeText.Int(args, "nextYearPercent");
        if (current is null || account is null || next is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new SetDevelopmentSplitCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            CurrentPercent = current.Value,
            AccountPercent = account.Value,
            NextYearPercent = next.Value,
            AeroPriority = BridgeText.Int(args, "aeroPriority") ?? DevelopmentEstimates.DefaultPriority,
            ChassisPriority = BridgeText.Int(args, "chassisPriority") ?? DevelopmentEstimates.DefaultPriority,
            ReliabilityPriority = BridgeText.Int(args, "reliabilityPriority") ?? DevelopmentEstimates.DefaultPriority,
            TyresPriority = BridgeText.Int(args, "tyresPriority") ?? DevelopmentEstimates.DefaultPriority,
        };
        return true;
    }

    private static bool Project(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, bool commit, out ICommand? command, out string? key)
    {
        command = null;
        var project = BridgeText.Required(args, "projectId");
        if (project is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = commit
            ? new CommitConceptCommand { ManagerId = manager, IssuedOn = today, OrganizationId = organization, ProjectId = project }
            : command;
        if (!commit)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        return true;
    }

    private static bool Approve(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new ApproveConceptCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            AeroMilli = BridgeText.Int(args, "aeroMilli") ?? 0,
            PhilosophyMilli = BridgeText.Int(args, "philosophyMilli") ?? 0,
            WindowMilli = BridgeText.Int(args, "windowMilli") ?? 0,
            CoolingMilli = BridgeText.Int(args, "coolingMilli") ?? 0,
            TyreMilli = BridgeText.Int(args, "tyreMilli") ?? 0,
            IntegrationMilli = BridgeText.Int(args, "integrationMilli") ?? 0,
        };
        return true;
    }

    private static bool Fund(JsonElement args, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var handle = BridgeText.Required(args, "personHandle");
        var programme = BridgeText.Required(args, "programme");
        if (handle is null || programme is null || !Enum.TryParse(programme, out JuniorProgramme parsed) || !Enum.IsDefined(parsed))
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        command = new FundJuniorCommand { ManagerId = manager, IssuedOn = today, PersonHandle = handle, Programme = parsed };
        key = null;
        return true;
    }

    private static bool SignPool(JsonElement args, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var handle = BridgeText.Required(args, "personHandle");
        var role = BridgeText.Required(args, "role");
        if (handle is null || role is null || !Enum.TryParse(role, out PoolSigningRole parsed) || !Enum.IsDefined(parsed))
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        command = new SignPoolDriverCommand { ManagerId = manager, IssuedOn = today, PersonHandle = handle, Role = parsed };
        key = null;
        return true;
    }

    private static bool Supply(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var supplier = BridgeText.Required(args, "supplierId");
        var item = BridgeText.Required(args, "item");
        var kind = BridgeText.Required(args, "kind");
        var season = BridgeText.Int(args, "firstSeason");
        var price = BridgeText.Long(args, "annualPriceCents");
        var seasons = BridgeText.Int(args, "seasons");
        var exclusive = BridgeText.Bool(args, "exclusive") ?? false;
        if (supplier is null || item is null || kind is null || season is null || price is null || seasons is null
            || !Enum.TryParse(item, out SupplyItem parsedItem) || !Enum.IsDefined(parsedItem)
            || !Enum.TryParse(kind, out SupplyKind parsedKind) || !Enum.IsDefined(parsedKind))
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new ProposeSupplyDealCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            SupplierId = supplier,
            Item = parsedItem,
            Kind = parsedKind,
            FirstSeason = season.Value,
            AnnualPriceCents = price.Value,
            Seasons = seasons.Value,
            Exclusive = exclusive,
            NegotiationId = BridgeText.Optional(args, "negotiationId") ?? string.Empty,
        };
        return true;
    }

    private static bool SupplyRespond(JsonElement args, CareerBridge career, HostManagerId manager, DateOnly today, out ICommand? command, out string? key)
    {
        command = null;
        var id = BridgeText.Required(args, "negotiationId");
        var accept = BridgeText.Bool(args, "accept");
        if (id is null || accept is null)
        {
            key = BridgeKeys.BadMessage;
            return false;
        }

        if (!Team(args, career, out var organization, out key))
        {
            return false;
        }

        command = new RespondToSupplyOfferCommand
        {
            ManagerId = manager,
            IssuedOn = today,
            OrganizationId = organization,
            NegotiationId = id,
            Accept = accept.Value,
        };
        return true;
    }

    private static bool Organization(JsonElement args, CareerBridge career, out OrganizationId organization, out string? key)
    {
        if (!Team(args, career, out var text, out key) || !BridgeIds.TryOrganization(text, out organization))
        {
            organization = default;
            key ??= BridgeKeys.BadMessage;
            return false;
        }

        return true;
    }

    private static bool Team(JsonElement args, CareerBridge career, out string organization, out string? key)
    {
        organization = BridgeText.Optional(args, "organizationId") ?? career.TeamId ?? string.Empty;
        if (organization.Length == 0)
        {
            key = PlayKeys.StateNoTeam;
            return false;
        }

        key = null;
        return true;
    }
}

internal static class BridgeIds
{
    public static bool TryPerson(string text, out PersonId id)
    {
        if (text.StartsWith("gen:", StringComparison.Ordinal)
            && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            && sequence >= 1)
        {
            id = PersonId.Generated(sequence);
            return true;
        }

        try
        {
            id = PersonId.Real(text);
            return true;
        }
        catch (ArgumentException)
        {
            id = default;
            return false;
        }
    }

    public static bool TryContract(string text, out ContractId id)
    {
        if (text.StartsWith("con:", StringComparison.Ordinal)
            && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            && sequence >= 1)
        {
            id = ContractId.Generated(sequence);
            return true;
        }

        id = default;
        return false;
    }

    public static bool TryOrganization(string text, out OrganizationId organization)
    {
        if (text.StartsWith("org:", StringComparison.Ordinal)
            && long.TryParse(text.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            && sequence >= 1)
        {
            organization = OrganizationId.Generated(sequence);
            return true;
        }

        try
        {
            organization = OrganizationId.Real(text);
            return true;
        }
        catch (ArgumentException)
        {
            organization = default;
            return false;
        }
    }
}

internal static class BridgeText
{
    public static string? Required(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string? Optional(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public static int? Int(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    public static long? Long(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    public static bool? Bool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
