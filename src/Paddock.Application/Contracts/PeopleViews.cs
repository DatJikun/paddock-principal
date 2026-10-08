using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// What the salary slider of an offer form shows (#265): the whole range it spans and the suggested range inside it. All amounts
/// are whole currency units per season. They come from what the asking organization believes about the person (the bands of its
/// scouts), never from the true attributes (INV-003). Every share is an ESTIMATE (<see cref="NegotiationEstimates"/>).
/// </summary>
public sealed record SalaryGuideView(long Min, long SuggestedLow, long Suggested, long SuggestedHigh, long Max);

/// <summary>Reads that several people screens share: the overall of a person and the salary guide. Pure queries (INV-005).</summary>
public static class PeopleViews
{
    /// <summary>
    /// The salary slider for one person and role, from the reference salary the organization believes the person is worth.
    /// </summary>
    public static SalaryGuideView SalaryGuide(long reference)
    {
        var suggested = Math.Max(1L, reference);
        return new SalaryGuideView(
            Share(suggested, NegotiationEstimates.SalarySliderMinShare),
            Share(suggested, NegotiationEstimates.SalarySuggestedLowShare),
            suggested,
            Share(suggested, NegotiationEstimates.SalarySuggestedHighShare),
            Share(suggested, NegotiationEstimates.SalarySliderMaxShare));
    }

    /// <summary>
    /// The overall of a person on the 1 to 20 scale of the attributes: the mean of the middle of the believed bands of the
    /// attributes the role uses, rounded. Null when the organization believes nothing about the person.
    /// </summary>
    public static int? Overall(NegotiationSubject subject, PersonKnowledgeView? knowledge)
    {
        if (knowledge is not PersonKnowledgeView view)
        {
            return null;
        }

        IReadOnlyList<string> keys = subject.Kind == NegotiationSubjectKind.DriverSeat
            ? GenerationEstimates.DriverAttributeKeys
            : StaffCatalogue.AttributeKeys(subject.StaffRole);
        var total = 0.0;
        var count = 0;
        foreach (var attribute in view.Attributes)
        {
            if (keys.Contains(attribute.Key))
            {
                total += (attribute.Band.Low + attribute.Band.High) / 2.0;
                count++;
            }
        }

        return count == 0 ? null : (int)Math.Round(total / count, MidpointRounding.AwayFromZero);
    }

    /// <summary>The first day a contract signed today would run from: the day after the one the person holds ends, or today.</summary>
    public static GameDate StartIfSignedToday(ContractBook book, PersonId person, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(book);
        var live = book.LiveContractsOf(person, today);
        return live.Count == 0 ? today : live[^1].End.AddDays(1);
    }

    private static long Share(long value, double share) => (long)Math.Round(value * share, MidpointRounding.AwayFromZero);
}
