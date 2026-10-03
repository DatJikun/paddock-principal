using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.World;

/// <summary>
/// The world-section registry: sections ride in <see cref="WorldState"/>, are hashed after the existing content
/// (<c>paddock-world/2</c>), and a world without sections hashes exactly as before (<c>paddock-world/1</c>).
/// </summary>
public class WorldSectionTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void AWorldWithNoSectionsKeepsTheVersionOneHash()
    {
        var plain = WorldFixtures.Small();
        var withAndWithout = plain.WithSection(new NoteSection("notes", 1, "a")).WithoutSection("notes");

        Assert.Equal(plain.StateHash(), withAndWithout.StateHash());
        Assert.Empty(withAndWithout.Sections);

        // The same digest the T15 hash test pins for its own fixture format, so version 1 itself did not move.
        Assert.Equal(
            "c209cf530de2740b796ebb52fce583461c47a356953d5dd6f1c4116164c71968",
            WorldStateHashTests.Build(reversed: false).StateHash());
    }

    [Fact]
    public void ASectionChangesTheHashAndTheSameSectionHashesTheSameEveryTime()
    {
        var world = WorldState.At(Opening);
        var section = new NoteSection("notes", 1, "alpha", "beta");

        var first = world.WithSection(section).StateHash();
        var second = WorldState.At(Opening).WithSection(new NoteSection("notes", 1, "alpha", "beta")).StateHash();

        Assert.Equal(first, second);
        Assert.NotEqual(world.StateHash(), first);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void TheDigestOfAWorldWithASectionIsPinned()
    {
        // paddock-world/2, checked against an independent SHA-256 of the documented text. If this fails after an intended format change, bump the format name and re-pin.
        var world = WorldState.At(Opening).WithSection(new NoteSection("notes", 1, "alpha", "beta"));

        Assert.Equal(PinnedSectionDigest, world.StateHash());
    }

    private const string PinnedSectionDigest = "3e59b2b3713fcc11278e10c49194c36b18c8baba246360f278f92638607beaac";

    [Fact]
    public void TheHashChangesWhenAnyPartOfASectionChanges()
    {
        var world = WorldState.At(Opening);
        var baseline = world.WithSection(new NoteSection("notes", 1, "alpha", "beta")).StateHash();

        Assert.NotEqual(baseline, world.WithSection(new NoteSection("notes", 1, "alpha", "gamma")).StateHash());
        Assert.NotEqual(baseline, world.WithSection(new NoteSection("notes", 1, "alpha")).StateHash());
        Assert.NotEqual(baseline, world.WithSection(new NoteSection("notes", 2, "alpha", "beta")).StateHash());
        Assert.NotEqual(baseline, world.WithSection(new NoteSection("other", 1, "alpha", "beta")).StateHash());
        Assert.NotEqual(baseline, world.WithSection(new NoteSection("notes", 1, "alphabeta")).StateHash());
        Assert.NotEqual(baseline, world.WithSection(new NoteSection("notes", 1, "alpha", "beta")).WithDate(Opening.AddDays(1)).StateHash());
    }

    [Fact]
    public void SectionTextWithLinesInItCannotImpersonateAnotherSection()
    {
        // The body is length-prefixed, so a note that looks like the next section header changes nothing but itself.
        var world = WorldState.At(Opening);
        var forged = world.WithSection(new NoteSection("notes", 1, "x\nsection 5:other 1\nbody 0:"));
        var honest = world.WithSection(new NoteSection("notes", 1, "x")).WithSection(new NoteSection("other", 1));

        Assert.NotEqual(honest.StateHash(), forged.StateHash());
    }

    [Fact]
    public void SectionsHashInNameOrderWhateverTheOrderTheyWereAdded()
    {
        var one = WorldState.At(Opening)
            .WithSection(new NoteSection("zeta", 1, "z"))
            .WithSection(new NoteSection("alpha", 1, "a"));
        var other = WorldState.At(Opening)
            .WithSection(new NoteSection("alpha", 1, "a"))
            .WithSection(new NoteSection("zeta", 1, "z"));

        Assert.Equal(one.StateHash(), other.StateHash());
        Assert.Equal(["alpha", "zeta"], one.Sections.Select(section => section.Name));
    }

    [Fact]
    public void TheHashDoesNotDependOnTheCurrentCulture()
    {
        var expected = WorldState.At(Opening).WithSection(new NoteSection("notes", 1, "alpha")).StateHash();
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "tr-TR", "pl-PL", "ar-SA" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                Assert.Equal(expected, WorldState.At(Opening).WithSection(new NoteSection("notes", 1, "alpha")).StateHash());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void WithSectionReplacesByNameAndLeavesTheEarlierWorldAlone()
    {
        var before = WorldState.At(Opening);
        var first = before.WithSection(new NoteSection("notes", 1, "alpha"));
        var second = first.WithSection(new NoteSection("notes", 1, "beta"));

        Assert.Empty(before.Sections);
        Assert.Equal(["alpha"], first.Section<NoteSection>("notes")!.Notes);
        Assert.Equal(["beta"], second.Section<NoteSection>("notes")!.Notes);
        Assert.Single(second.Sections);
        Assert.Null(second.Section("missing"));
    }

    [Fact]
    public void OtherEditsKeepTheSections()
    {
        var world = WorldFixtures.Small().WithSection(new NoteSection("notes", 1, "alpha"));
        var edited = world.WithDate(world.CurrentDate.AddDays(1));
        var (added, _) = edited.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, false, null, Opening, null, 1, [new OrganizationNameSpan("Fixture Racing", Opening, null)]));

        Assert.Equal(["alpha"], added.Section<NoteSection>("notes")!.Notes);
    }

    [Fact]
    public void ASectionOfTheWrongTypeIsAnErrorNotANull()
    {
        var world = WorldState.At(Opening).WithSection(new NoteSection("notes", 1, "alpha"));

        Assert.Throws<InvalidOperationException>(() => world.Section<OtherSection>("notes"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Notes")]
    [InlineData("1notes")]
    [InlineData("no tes")]
    [InlineData("no_tes")]
    public void SectionNamesAreLowercaseLettersDigitsDotsAndHyphens(string name)
    {
        Assert.False(SectionNames.IsValid(name));
        Assert.Throws<ArgumentException>(() => WorldState.At(Opening).WithSection(new NoteSection(name, 1)));
    }

    [Fact]
    public void ASectionNeedsASchemaVersionOfAtLeastOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldState.At(Opening).WithSection(new NoteSection("notes", 0)));
    }

    [Fact]
    public void RestoreTakesSectionsAndRejectsTwoWithOneName()
    {
        var world = WorldFixtures.Small();
        var restored = WorldState.Restore(
            world.CurrentDate,
            world.Ids,
            world.Persons,
            world.Organizations,
            world.Contracts,
            world.Knowledge,
            [new NoteSection("notes", 1, "alpha")]);

        Assert.Equal(world.WithSection(new NoteSection("notes", 1, "alpha")).StateHash(), restored.StateHash());
        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            world.CurrentDate,
            world.Ids,
            world.Persons,
            world.Organizations,
            world.Contracts,
            world.Knowledge,
            [new NoteSection("notes", 1), new NoteSection("notes", 1)]));
    }

    private sealed class NoteSection : IWorldSection
    {
        public NoteSection(string name, int schemaVersion, params string[] notes)
        {
            Name = name;
            SchemaVersion = schemaVersion;
            Notes = notes;
        }

        public string Name { get; }

        public int SchemaVersion { get; }

        public IReadOnlyList<string> Notes { get; }

        public void WriteCanonical(CanonicalWriter writer)
        {
            writer.Count("notes", Notes.Count);
            foreach (var note in Notes)
            {
                writer.TextLine("note", note);
            }
        }
    }

    private sealed class OtherSection : IWorldSection
    {
        public string Name => "notes";

        public int SchemaVersion => 1;

        public void WriteCanonical(CanonicalWriter writer)
        {
        }
    }
}
