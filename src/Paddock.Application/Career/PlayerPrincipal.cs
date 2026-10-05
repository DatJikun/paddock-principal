using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>
/// The person the player is (PP-044): a team principal with a fixed average and one optional tilt (ESTIMATE,
/// <see cref="PlayerEstimates"/>). Truth and the hiring team's belief are built together so the belief is a band,
/// never a copy of <see cref="PersonTruth"/> handed to a query.
/// </summary>
public static class PlayerPrincipal
{
    /// <summary>
    /// The attribute the tilt names, or null for <see cref="PlayerEstimates.NoTilt"/>. False when the word is not one of
    /// the principal's attributes.
    /// </summary>
    public static bool TryTilt(string text, out string? attribute)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.Equals(text, PlayerEstimates.NoTilt, StringComparison.Ordinal))
        {
            attribute = null;
            return true;
        }

        foreach (var key in StaffCatalogue.AttributeKeys(StaffRole.TeamPrincipal))
        {
            if (string.Equals(key, text, StringComparison.Ordinal))
            {
                attribute = key;
                return true;
            }
        }

        attribute = null;
        return false;
    }

    public static PersonTruth Truth(string? tiltAttribute)
    {
        var keys = StaffCatalogue.AttributeKeys(StaffRole.TeamPrincipal);
        var attributes = new NamedAttribute[keys.Count];
        for (var i = 0; i < keys.Count; i++)
        {
            var value = PlayerEstimates.Average;
            if (tiltAttribute is not null && string.Equals(keys[i], tiltAttribute, StringComparison.Ordinal))
            {
                value = Math.Min(GenerationEstimates.AttributeMax, PlayerEstimates.Average + PlayerEstimates.TiltBonus);
            }

            attributes[i] = new NamedAttribute(keys[i], value);
        }

        // Fixed at creation: the ceiling is the value itself (ESTIMATE). Nothing here grows later by itself.
        return new PersonTruth(attributes, attributes.ToArray());
    }

    public static PersonSpec Spec(string givenName, string familyName, string nationality, int startYear, string? tiltAttribute)
    {
        var birthYear = startYear - PlayerEstimates.AgeAtStart;
        if (birthYear < GenerationEstimates.MinBirthYear)
        {
            birthYear = GenerationEstimates.MinBirthYear;
        }

        return new PersonSpec(
            givenName,
            familyName,
            new GameDate(birthYear, 1, 1),
            nationality,
            isReal: false,
            realId: null,
            [PersonRole.TeamPrincipal],
            Truth(tiltAttribute));
    }

    /// <summary>What the hiring team believes: a band of one value for each attribute. Rivals are not given a belief.</summary>
    public static PersonKnowledge Knowledge(OrganizationId team, PersonId person, PersonTruth truth)
    {
        var bands = new KnownAttribute[truth.Attributes.Count];
        for (var i = 0; i < bands.Length; i++)
        {
            var value = truth.Attributes[i].Value;
            bands[i] = new KnownAttribute(truth.Attributes[i].Key, new AttributeBand(value, value));
        }

        return new PersonKnowledge(team, person, bands, null);
    }
}
