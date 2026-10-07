using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Infrastructure;

/// <summary>
/// Facilities and booked tests of every team, section <see cref="SectionName"/>. Immutable. Truth stays here; a manager
/// reads <c>InfrastructureQuery</c>. A save with no infrastructure has no section.
/// <para>
/// Canonical text (schema 1), after the section header. Quality is milli-units, money is integer cents.
/// <code>
/// facilities &lt;count&gt;
/// facility &lt;len&gt;:&lt;org&gt; &lt;kind&gt; &lt;quality&gt; &lt;started or -&gt; &lt;ends or -&gt; &lt;target or -&gt; &lt;cost&gt;
/// tests &lt;count&gt;
/// test &lt;len&gt;:&lt;org&gt; &lt;date&gt; &lt;cost&gt;
/// </code>
/// </para>
/// </summary>
public sealed class InfrastructureSection : IWorldSection
{
    public const string SectionName = "infrastructure";

    private readonly SortedDictionary<string, Facility> _facilities;
    private readonly TestBooking[] _tests;

    private InfrastructureSection(SortedDictionary<string, Facility> facilities, TestBooking[] tests)
    {
        _facilities = facilities;
        _tests = tests;
    }

    public static InfrastructureSection Empty { get; } = new(
        new SortedDictionary<string, Facility>(StringComparer.Ordinal),
        []);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _facilities.Count == 0 && _tests.Length == 0;

    public IReadOnlyList<Facility> Facilities => _facilities.Values.ToArray();

    public IReadOnlyList<TestBooking> Tests => _tests;

    public Facility? Find(OrganizationId organization, FacilityKind kind) =>
        organization.IsAssigned && _facilities.TryGetValue(Key(organization, kind), out var facility) ? facility : null;

    public IReadOnlyList<Facility> Of(OrganizationId organization)
    {
        if (!organization.IsAssigned)
        {
            return [];
        }

        return _facilities.Values.Where(facility => facility.Organization == organization).ToArray();
    }

    public IReadOnlyList<TestBooking> TestsOf(OrganizationId organization, int year)
    {
        if (!organization.IsAssigned)
        {
            return [];
        }

        return _tests.Where(test => test.Organization == organization && test.Date.Year == year).ToArray();
    }

    public int TestsUsed(OrganizationId organization, int year) => TestsOf(organization, year).Count;

    public InfrastructureSection Upsert(Facility facility)
    {
        ArgumentNullException.ThrowIfNull(facility);
        Validate(facility);
        var copy = CopyFacilities();
        copy[Key(facility.Organization, facility.Kind)] = facility;
        return new InfrastructureSection(copy, _tests);
    }

    public InfrastructureSection AddTest(TestBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        Validate(booking);
        var next = new TestBooking[_tests.Length + 1];
        Array.Copy(_tests, next, _tests.Length);
        next[_tests.Length] = booking;
        Array.Sort(next, CompareTests);
        return new InfrastructureSection(CopyFacilities(), next);
    }

    /// <summary>Drops one booking that has not happened yet. Throws when the booking is not there.</summary>
    public InfrastructureSection RemoveTest(TestBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        var index = Array.IndexOf(_tests, booking);
        if (index < 0)
        {
            throw new InvalidOperationException("The test booking is not in the section.");
        }

        var next = new TestBooking[_tests.Length - 1];
        Array.Copy(_tests, 0, next, 0, index);
        Array.Copy(_tests, index + 1, next, index, _tests.Length - index - 1);
        return new InfrastructureSection(CopyFacilities(), next);
    }

    public static InfrastructureSection Restore(IEnumerable<Facility> facilities, IEnumerable<TestBooking>? tests = null)
    {
        ArgumentNullException.ThrowIfNull(facilities);
        var map = new SortedDictionary<string, Facility>(StringComparer.Ordinal);
        foreach (var facility in facilities)
        {
            ArgumentNullException.ThrowIfNull(facility);
            Validate(facility);
            var key = Key(facility.Organization, facility.Kind);
            if (!map.TryAdd(key, facility))
            {
                throw new InvalidOperationException(
                    $"Organization '{facility.Organization}' has two '{facility.Kind}' facilities.");
            }
        }

        var booked = (tests ?? []).ToArray();
        foreach (var booking in booked)
        {
            ArgumentNullException.ThrowIfNull(booking);
            Validate(booking);
        }

        Array.Sort(booked, CompareTests);
        return new InfrastructureSection(map, booked);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("facilities", _facilities.Count);
        foreach (var facility in _facilities.Values)
        {
            writer.Begin("facility");
            writer.Field(facility.Organization.Value);
            writer.Raw(
                " " + facility.Kind
                + " " + N(facility.QualityMilli)
                + " " + D(facility.BuildStarted)
                + " " + D(facility.BuildEnds)
                + " " + (facility.TargetQualityMilli is { } target ? N(target) : "-")
                + " " + N(facility.CostCents));
            writer.End();
        }

        writer.Count("tests", _tests.Length);
        foreach (var test in _tests)
        {
            writer.Begin("test");
            writer.Field(test.Organization.Value);
            writer.Raw(" " + test.Date + " " + N(test.CostCents));
            writer.End();
        }
    }

    private SortedDictionary<string, Facility> CopyFacilities()
    {
        var copy = new SortedDictionary<string, Facility>(StringComparer.Ordinal);
        foreach (var (key, facility) in _facilities)
        {
            copy[key] = facility;
        }

        return copy;
    }

    private static string Key(OrganizationId organization, FacilityKind kind) => organization.Value + "|" + kind;

    private static int CompareTests(TestBooking left, TestBooking right)
    {
        var org = string.Compare(left.Organization.Value, right.Organization.Value, StringComparison.Ordinal);
        if (org != 0)
        {
            return org;
        }

        var date = left.Date.CompareTo(right.Date);
        return date != 0 ? date : left.CostCents.CompareTo(right.CostCents);
    }

    private static void Validate(Facility facility)
    {
        if (!facility.Organization.IsAssigned
            || !Enum.IsDefined(facility.Kind)
            || facility.QualityMilli is < 0 or > InfrastructureEstimates.MaxQualityMilli
            || facility.CostCents < 0
            || (facility.TargetQualityMilli is { } target
                && (target < facility.QualityMilli || target > InfrastructureEstimates.MaxQualityMilli))
            || facility.IsBuilding != facility.BuildStarted is not null
            || facility.IsBuilding != facility.TargetQualityMilli is not null
            || (facility.BuildStarted is { } started && facility.BuildEnds is { } ends && ends <= started))
        {
            throw new InvalidOperationException(
                $"The '{facility.Kind}' facility of '{facility.Organization}' is out of range.");
        }
    }

    private static void Validate(TestBooking booking)
    {
        if (!booking.Organization.IsAssigned || booking.CostCents < 1)
        {
            throw new InvalidOperationException(
                $"The test booking of '{booking.Organization}' is out of range.");
        }
    }

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string D(GameDate? date) => date?.ToString() ?? "-";
}
