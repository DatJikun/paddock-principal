using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.Pool;

/// <summary>The pool section: membership, handles, canonical text. Ids and dates are synthetic.</summary>
public class TalentPoolSectionTests
{
    private static readonly GameDate Day = new(1950, 1, 1);

    private static readonly PersonId[] People =
    [
        PersonId.Real("aa"),
        PersonId.Real("bb"),
        PersonId.Real("cc"),
        PersonId.Generated(1),
        PersonId.Generated(2),
        PersonId.Generated(3),
    ];

    [Fact]
    public void HandlesDoNotDependOnTheOrderOfTheBatchAndDoNotFollowTheIds()
    {
        var forward = TalentPoolSection.Empty.EnterAll(People, Day);
        var backward = TalentPoolSection.Empty.EnterAll(People.Reverse(), Day);

        Assert.Equal(Canonical(forward), Canonical(backward));
        var byId = forward.Members.Select(member => member.Handle).ToArray();
        Assert.NotEqual(byId.Order().ToArray(), byId);
        Assert.Equal([1L, 2, 3, 4, 5, 6], byId.Order());
    }

    [Fact]
    public void HandlesAreNeverReusedAfterAMemberLeaves()
    {
        var section = TalentPoolSection.Empty.EnterAll(People.Take(2), Day);
        var first = section.Members.Max(member => member.Handle);
        section = section.Leave(People[0]).Lapse(People[1], Day).EnterAll([People[3]], Day.AddDays(1));

        Assert.Equal(first + 1, section.Members.Single().Handle);
        Assert.Equal(first + 2, section.NextHandle);
        Assert.Null(section.FindByHandle(TalentPoolSection.HandleOf(1)));
    }

    [Fact]
    public void AHandleIsFoundOnlyInItsCanonicalForm()
    {
        var section = TalentPoolSection.Empty.EnterAll(People, Day);
        var member = section.Members[0];
        Assert.Equal(member.Id, section.FindByHandle(member.HandleText)!.Id);
        Assert.Null(section.FindByHandle("talent-007"));
        Assert.Null(section.FindByHandle("talent-" + member.Handle + " "));
        Assert.Null(section.FindByHandle(member.Id.Value));
        Assert.Null(section.FindByHandle(""));
        Assert.DoesNotContain("gen", member.HandleText, StringComparison.Ordinal);
    }

    [Fact]
    public void ALapsedPersonCannotEnterAgain()
    {
        var section = TalentPoolSection.Empty.EnterAll([People[0]], Day).Lapse(People[0], Day.AddDays(400));

        Assert.True(section.HasLapsed(People[0]));
        Assert.Equal(new GameDate(1951, 2, 5), section.Lapsed.Single().On);
        Assert.Throws<InvalidOperationException>(() => section.EnterAll([People[0]], Day));
        Assert.Throws<InvalidOperationException>(() => TalentPoolSection.Empty.EnterAll([People[0], People[0]], Day));
    }

    [Fact]
    public void LeavingEndsFocusOnAndObservationOfThePerson()
    {
        var section = TalentPoolSection.Empty.EnterAll(People.Take(2), Day)
            .SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, People[0]))
            .SetFocus(new ScoutFocus(PoolKit.Bravo, ScoutFocusKind.Pool, null))
            .AddObservation(PoolKit.Alpha, People[0], 500)
            .AddObservation(PoolKit.Alpha, People[1], 700)
            .AddObservation(PoolKit.Alpha, People[1], 300);

        Assert.Equal(1000, section.ObservedMilli(PoolKit.Alpha, People[1]));
        var after = section.Leave(People[0]);

        Assert.Null(after.FocusOf(PoolKit.Alpha));
        Assert.Equal(ScoutFocusKind.Pool, after.FocusOf(PoolKit.Bravo)!.Kind);
        Assert.Equal(0, after.ObservedMilli(PoolKit.Alpha, People[0]));
        Assert.Equal(1000, after.ObservedMilli(PoolKit.Alpha, People[1]));
        Assert.Same(after, after.Leave(People[0]));
    }

    [Fact]
    public void ADrawnFocusReplacesTheEarlierOne()
    {
        var section = TalentPoolSection.Empty.EnterAll(People.Take(2), Day)
            .SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, People[0]))
            .SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Pool, null));

        Assert.Equal(ScoutFocusKind.Pool, section.FocusOf(PoolKit.Alpha)!.Kind);
        Assert.Single(section.Focuses);
        Assert.Throws<InvalidOperationException>(() =>
            section.SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, People[5])));
        Assert.Throws<ArgumentException>(() => new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, null));
        Assert.Throws<ArgumentException>(() => new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Pool, People[0]));
    }

    [Fact]
    public void AMemberHasOneFundedSeasonAtATime()
    {
        var funding = new JuniorFunding(PoolKit.Alpha, JuniorProgramme.CheapSlow, 1950);
        var section = TalentPoolSection.Empty.EnterAll([People[0]], Day).Recruit(People[0], PoolKit.Alpha).Fund(People[0], funding);

        Assert.Equal(funding, section.Find(People[0])!.Funding);
        Assert.Throws<InvalidOperationException>(() => section.Fund(People[0], funding));
        Assert.Null(section.ClearFunding(People[0]).Find(People[0])!.Funding);
        Assert.Throws<InvalidOperationException>(() => section.Fund(People[1], funding));
    }

    [Fact]
    public void TheCanonicalTextChangesWithEveryField()
    {
        var baseline = TalentPoolSection.Empty.EnterAll(People.Take(3), Day);
        var texts = new[]
        {
            Canonical(baseline),
            Canonical(baseline.Recruit(People[0], PoolKit.Alpha).Fund(People[0], new JuniorFunding(PoolKit.Alpha, JuniorProgramme.CheapSlow, 1950))),
            Canonical(baseline.Recruit(People[0], PoolKit.Alpha).Fund(People[0], new JuniorFunding(PoolKit.Alpha, JuniorProgramme.ExpensiveFast, 1950))),
            Canonical(baseline.Recruit(People[0], PoolKit.Bravo).Fund(People[0], new JuniorFunding(PoolKit.Bravo, JuniorProgramme.CheapSlow, 1950))),
            Canonical(baseline.Recruit(People[0], PoolKit.Alpha).Fund(People[0], new JuniorFunding(PoolKit.Alpha, JuniorProgramme.CheapSlow, 1951))),
            Canonical(baseline.SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Pool, null))),
            Canonical(baseline.SetFocus(new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, People[0]))),
            Canonical(baseline.AddObservation(PoolKit.Alpha, People[0], 1)),
            Canonical(baseline.AddObservation(PoolKit.Alpha, People[0], 2)),
            Canonical(baseline.Lapse(People[0], Day)),
            Canonical(baseline.Leave(People[0])),
            Canonical(baseline.EnterAll([People[4]], Day)),
            Canonical(baseline.EnterAll([People[4]], Day.AddDays(1))),
        };

        Assert.Equal(texts.Length, texts.Distinct().Count());
        Assert.Equal(Canonical(baseline), Canonical(TalentPoolSection.Empty.EnterAll(People.Take(3).Reverse(), Day)));
    }

    [Fact]
    public void ARestoredSectionHasTheSameText()
    {
        var section = TalentPoolSection.Empty.EnterAll(People, Day)
            .Recruit(People[0], PoolKit.Alpha)
            .Fund(People[0], new JuniorFunding(PoolKit.Alpha, JuniorProgramme.ExpensiveFast, 1950))
            .SetFocus(new ScoutFocus(PoolKit.Bravo, ScoutFocusKind.Person, People[1]))
            .AddObservation(PoolKit.Bravo, People[1], 2500)
            .Lapse(People[2], Day.AddDays(366));

        var restored = TalentPoolSection.Restore(section.NextHandle, section.Members, section.Lapsed, section.Focuses, section.Observations);

        Assert.Equal(Canonical(section), Canonical(restored));
    }

    [Fact]
    public void RestoreRefusesInconsistentRows()
    {
        var member = new PoolMember(People[0], 1, Day, null);
        Assert.Throws<InvalidOperationException>(() => Restore(1, [member]));
        Assert.Throws<InvalidOperationException>(() => Restore(5, [member, member]));
        Assert.Throws<InvalidOperationException>(() => Restore(5, [member, new PoolMember(People[1], 1, Day, null)]));
        Assert.Throws<InvalidOperationException>(() =>
            TalentPoolSection.Restore(5, [member], [new LapsedCareer(People[0], Day)], [], []));
        Assert.Throws<InvalidOperationException>(() =>
            TalentPoolSection.Restore(5, [member], [], [new ScoutFocus(PoolKit.Alpha, ScoutFocusKind.Person, People[1])], []));
        Assert.Throws<InvalidOperationException>(() =>
            TalentPoolSection.Restore(5, [member], [], [], [new ObservationRow(PoolKit.Alpha, People[1], 5)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Restore(0, []));
    }

    [Fact]
    public void TheSectionIsRegisteredByItsOwnNameAndVersion()
    {
        Assert.True(SectionNames.IsValid(TalentPoolSection.SectionName));
        Assert.Equal(2, TalentPoolSection.Empty.SchemaVersion);
        var world = PoolKit.EmptyWorld().WithSection(TalentPoolSection.Empty.EnterAll(People.Take(1), Day));
        Assert.NotEqual(PoolKit.EmptyWorld().StateHash(), world.StateHash());
    }

    private static TalentPoolSection Restore(long next, IEnumerable<PoolMember> members) =>
        TalentPoolSection.Restore(next, members, [], [], []);

    private static string Canonical(TalentPoolSection section)
    {
        var writer = new CanonicalWriter();
        section.WriteCanonical(writer);
        return writer.ToString();
    }
}
