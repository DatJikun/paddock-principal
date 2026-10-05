namespace Paddock.Application.Career;

/// <summary>
/// Uncalibrated numbers for the player principal created when a career starts (issue #112, the stated default for
/// open question 2). Every value is an ESTIMATE, not a measured fact.
/// </summary>
public static class PlayerEstimates
{
    /// <summary>ESTIMATE: the middle of the 1–20 scale. Every principal attribute starts here.</summary>
    public const int Average = 10;

    /// <summary>ESTIMATE: how far the one chosen tilt sits above <see cref="Average"/>, capped at 20.</summary>
    public const int TiltBonus = 4;

    /// <summary>ESTIMATE: age on 1 January of the start year. The birth date is 1 January of that year minus this age.</summary>
    public const int AgeAtStart = 40;

    /// <summary>The tilt word the wizard accepts when the player does not tilt any attribute.</summary>
    public const string NoTilt = "none";
}
