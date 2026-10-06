using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>One of the team's own facilities as the principal sees it: relative quality and the posted upgrade cost, never a rival's.</summary>
public sealed record FacilityCase(string Kind, double RelativeQuality, long UpgradeCostCents, bool Building, bool Eligible);

/// <summary>Posted rental cost and remaining tests this year. The AI does not read a rival's test programme.</summary>
public sealed record TestRentalCase(long CostCents, int Used, int Cap, bool Allowed);

/// <summary>What the infrastructure decider reads: cash, own facilities and the posted test rental.</summary>
public sealed record InfrastructureInput(
    DateOnly Today,
    long CashCents,
    IReadOnlyList<FacilityCase> Facilities,
    TestRentalCase Tests);

/// <summary>Which facility to upgrade, whether to book a test, or wait. Kinds and the book-test command match the player's.</summary>
public sealed record InfrastructureDecision(string? Kind, bool BookTest);

/// <summary>
/// Whether to spend on a factory upgrade or a private test (PP-026, PP-064). Uses only own relative quality, cash and posted
/// costs. Nobody reads a rival's factory or next year's frontier.
/// </summary>
public static class InfrastructureDecider
{
    public const string Kind = "infrastructure.upgrade";

    public const string BookTestOption = "book_test";

    public static InfrastructureDecision Review(DecisionContext context, InfrastructureInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var options = new List<OptionDraft>
        {
            new(AiTextKeys.OptionWait, [new FactorDraft(AiTextKeys.FactorInertia, context.Profile.Stability * 0.15)]),
        };
        foreach (var facility in input.Facilities.OrderBy(item => item.Kind, StringComparer.Ordinal))
        {
            if (!facility.Eligible || facility.Building || facility.UpgradeCostCents <= 0
                || input.CashCents < facility.UpgradeCostCents * 12
                || facility.RelativeQuality >= 0.40)
            {
                continue;
            }

            var gap = Math.Clamp(1.0 - facility.RelativeQuality, 0.0, 1.0);
            var price = input.CashCents <= 0 ? 1.0 : facility.UpgradeCostCents / (double)input.CashCents;
            options.Add(new OptionDraft(
                "upgrade/" + facility.Kind,
                [
                    new FactorDraft(AiTextKeys.FactorQuality, context.Profile.Future * gap),
                    new FactorDraft(AiTextKeys.FactorAffordability, -context.Profile.Thrift * price),
                    new FactorDraft(AiTextKeys.FactorResultsNow, context.Profile.Now * gap * 0.25),
                ]));
        }

        if (input.Tests.Allowed && input.Tests.CostCents > 0 && input.CashCents >= input.Tests.CostCents * 16)
        {
            var room = input.Tests.Cap <= 0 ? 0.0 : 1.0 - (input.Tests.Used / (double)input.Tests.Cap);
            var price = input.CashCents <= 0 ? 1.0 : input.Tests.CostCents / (double)input.CashCents;
            options.Add(new OptionDraft(
                BookTestOption,
                [
                    new FactorDraft(AiTextKeys.FactorQuality, context.Profile.Now * room * 0.4),
                    new FactorDraft(AiTextKeys.FactorAffordability, -context.Profile.Thrift * price),
                ]));
        }

        var note = new TraceNote(
            Kind,
            context.Season.ToString(CultureInfo.InvariantCulture),
            "season-" + context.Season.ToString(CultureInfo.InvariantCulture),
            "Weighs upgrading own facilities and renting a test against cash on " + input.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Development, note, options, id =>
            id.StartsWith("upgrade/", StringComparison.Ordinal)
                ? AiTextKeys.ReasonInfrastructure
                : id == BookTestOption
                    ? AiTextKeys.ReasonBookTest
                    : AiTextKeys.ReasonWaited);
        if (chosen.Id.StartsWith("upgrade/", StringComparison.Ordinal))
        {
            return new InfrastructureDecision(chosen.Id["upgrade/".Length..], false);
        }

        return new InfrastructureDecision(null, chosen.Id == BookTestOption);
    }
}
