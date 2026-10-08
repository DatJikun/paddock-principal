using System.Text.RegularExpressions;

namespace Paddock.Tests.Regulation;

/// <summary>
/// A team's political leaning is read in one place (#275, owner decision 3), so that when it later changes over time (by era, by the
/// car-industry situation) only that place changes. Anything else that reads the field fails here.
/// </summary>
public class LeaningReadTests
{
    private static readonly string[] Allowed =
    [
        // The seam itself.
        Path.Combine("Paddock.Simulation", "Regulation", "VoteInterest.cs"),

        // Keeping the field: its canonical text, its save and its own record.
        Path.Combine("Paddock.Domain", "Racing", "RegulationRecords.cs"),
        Path.Combine("Paddock.Domain", "Racing", "RegulationsSection.cs"),
        Path.Combine("Paddock.Persistence", "RegulationsSectionStore.cs"),
    ];

    [Fact]
    public void OnlyTheSeamReadsTheLeaningOfATeamToVote()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        var read = new Regex(@"(?<![A-Za-z])\.Leaning\b|\bLeaning\s*=>", RegexOptions.Compiled);
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file);
            if (relative.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Allowed.Contains(relative))
            {
                continue;
            }

            if (read.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(relative);
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheSeamIsTheOnlyPlaceTheAiTeamsAskForALeaning()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        var users = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("TeamLeaningSeam.Of(", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["RegulationPolitics.cs"], users);
    }
}
