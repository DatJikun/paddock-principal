using Paddock.Application.Localization;

namespace Paddock.Application.Cars;

/// <summary>
/// Player-facing keys of the car model. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// Every const is marked <c>[TranslationKey]</c> so <c>i18n-check</c> requires it in both files.
/// </summary>
public static class CarKeys
{
    [TranslationKey]
    public const string NoControl = "car.error.notInControl";

    [TranslationKey]
    public const string UnknownOrganization = "car.error.unknownOrganization";

    [TranslationKey]
    public const string NotATeam = "car.error.notATeam";

    [TranslationKey]
    public const string AxisRange = "car.error.axisRange";

    /// <summary>PP-050: customer chassis are out of the MVP.</summary>
    [TranslationKey]
    public const string CustomerNotInMvp = "car.customer.notInMvp";

    /// <summary>The regulation forbids a customer chassis even if the MVP later allows the path.</summary>
    [TranslationKey]
    public const string CustomerForbidden = "car.customer.forbidden";
}
