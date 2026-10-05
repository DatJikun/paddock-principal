using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// A driver asking for more money before the contract ends (PP-057). Pure: the caller supplies loyalty, morale and the
/// era benchmarks, and rolls its own child of <c>Market</c>. Nothing here reads truth (INV-003) or draws a number.
/// </summary>
public static class DriverRaise
{
    /// <summary>
    /// Chance of a demand this season. Loyalty is 1–20. Morale is 0 (unhappy) to 1 (happy). A loyal and happy driver
    /// sits on the floor; a disloyal and unhappy one approaches the floor plus the span.
    /// </summary>
    public static double AskChance(int loyalty, double morale)
    {
        var disloyal = (21d - Math.Clamp(loyalty, 1, 20)) / 20d;
        var unhappy = 1d - Math.Clamp(morale, 0d, 1d);
        return NegotiationEstimates.RaiseAskFloor + (NegotiationEstimates.RaiseAskSpan * disloyal * unhappy);
    }

    /// <summary>
    /// Extra salary asked. The gap is the era benchmark at the stars he can still reach minus the benchmark at the stars
    /// the team believes he has now. A driver already at that ceiling asks only <see cref="NegotiationEstimates.PeakRaiseShare"/>.
    /// This is not a market value (PP-035).
    /// </summary>
    public static long ExtraSalary(long currentSalary, long benchmarkNow, long benchmarkReachable)
    {
        if (currentSalary < 0 || benchmarkNow < 0 || benchmarkReachable < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentSalary), "Salaries and benchmarks are not negative.");
        }

        var gap = Math.Max(0L, benchmarkReachable - benchmarkNow);
        var floor = (long)Math.Round(currentSalary * NegotiationEstimates.PeakRaiseShare, MidpointRounding.AwayFromZero);
        return Math.Max(floor, gap);
    }

    /// <summary>The salary after meeting part of the gap. The rest of the demand is refused.</summary>
    public static long PartialSalary(long currentSalary, long askedSalary)
    {
        var extra = Math.Max(0L, askedSalary - currentSalary);
        return currentSalary + (long)Math.Round(extra * NegotiationEstimates.PartialRaiseShare, MidpointRounding.AwayFromZero);
    }

    /// <summary>An AI team accepts a demand that is small next to the salary it already pays.</summary>
    public static bool AiAccepts(long currentSalary, long extra) =>
        extra <= (long)Math.Round(Math.Max(0L, currentSalary) * NegotiationEstimates.AiRaiseAcceptShare, MidpointRounding.AwayFromZero);

    /// <summary>
    /// ESTIMATE stand-in for morale: points against the teammate and championship position against the expected one.
    /// No results (all zeros) is <see cref="NegotiationEstimates.NeutralMorale"/>.
    /// </summary>
    public static double Morale(int ownPoints, int teammatePoints, int position, int expectedPosition)
    {
        if (ownPoints == 0 && teammatePoints == 0 && position == 0 && expectedPosition == 0)
        {
            return NegotiationEstimates.NeutralMorale;
        }

        var versusMate = ownPoints >= teammatePoints ? 0.15d : -0.15d;
        var versusExpected = Math.Clamp((expectedPosition - position) / 10d, -0.35d, 0.35d);
        return Math.Clamp(NegotiationEstimates.NeutralMorale + versusMate + versusExpected, 0d, 1d);
    }

    /// <summary>Stars the team believes he can still reach, from the potential band, or the stars he has now when that band is unknown.</summary>
    public static double ReachableStars(PersonKnowledgeView? knowledge, double starsNow)
    {
        if (knowledge is PersonKnowledgeView view && view.Potential is AttributeBand band)
        {
            return NegotiationEstimates.StarsFromMean((band.Low + band.High) / 2d);
        }

        return starsNow;
    }
}

/// <summary>How happy a contracted driver is. Real morale is not in the world yet; a host supplies this stand-in (PP-057).</summary>
public interface IDriverMorale
{
    /// <summary>0 is unhappy, 1 is happy. A pure query (INV-005).</summary>
    double Happiness(WorldState world, PersonId person, OrganizationId team, GameDate today);
}

/// <summary>No results to read, so every driver is neither happy nor unhappy.</summary>
public sealed class NeutralDriverMorale : IDriverMorale
{
    public double Happiness(WorldState world, PersonId person, OrganizationId team, GameDate today) =>
        NegotiationEstimates.NeutralMorale;
}
