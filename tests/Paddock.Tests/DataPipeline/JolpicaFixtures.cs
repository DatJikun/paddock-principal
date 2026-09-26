namespace Paddock.Tests.DataPipeline;

internal static class JolpicaFixtures
{
    public static string Materialize()
    {
        var root = Path.Combine(Path.GetTempPath(), "paddock-jolpica-" + Guid.NewGuid().ToString("N"));
        Copy(root, "drivers.json", "raw", "drivers", "offset-0.json");
        Copy(root, "constructors.json", "raw", "constructors", "offset-0.json");
        Copy(root, "circuits.json", "raw", "circuits", "offset-0.json");
        Copy(root, "races-1950.json", "raw", "races", "1950", "offset-0.json");
        Copy(root, "races-1952.json", "raw", "races", "1952", "offset-0.json");
        Copy(root, "races-1994.json", "raw", "races", "1994", "offset-0.json");
        Copy(root, "races-2021.json", "raw", "races", "2021", "offset-0.json");
        Copy(root, "results-1950-offset-0.json", "raw", "results", "1950", "offset-0.json");
        Copy(root, "results-1950-offset-100.json", "raw", "results", "1950", "offset-100.json");
        Copy(root, "results-1950-offset-200.json", "raw", "results", "1950", "offset-200.json");
        Copy(root, "results-1952-dq.json", "raw", "results", "1952", "offset-0.json");
        Copy(root, "qualifying-1950-empty.json", "raw", "qualifying", "1950", "offset-0.json");
        Copy(root, "qualifying-1994.json", "raw", "qualifying", "1994", "offset-0.json");
        Copy(root, "sprint-2021.json", "raw", "sprint", "2021", "offset-0.json");
        return root;
    }

    public static string[] Lines(StringWriter writer)
    {
        return writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static void Copy(string root, string fixture, params string[] relative)
    {
        var source = Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "Fixtures", "jolpica", fixture);
        var destination = Path.Combine(new[] { root }.Concat(relative).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }
}
