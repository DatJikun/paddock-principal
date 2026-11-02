namespace Paddock.Tests;

/// <summary>
/// The one place that names the xUnit trait for slow tests (#358). Agents run the fast set
/// locally with <c>--filter "Category!=Slow"</c>; CI runs every test, Slow included.
/// A test goes to Slow because of its measured run time (ESTIMATE threshold: about 3 s
/// on a 4-core Linux box), never because it is less important: slow hash, determinism and
/// save/resume pins are moved here, not dropped.
/// </summary>
internal static class TestCategory
{
    public const string Name = "Category";

    public const string Slow = "Slow";
}
