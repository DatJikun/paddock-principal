using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// The data root every bridge test uses: a copy of <c>data/authored</c> in a temp folder, with no <c>cache</c>. A local Jolpica
/// cache (PP-041) changes the opening world (real race dates, real drivers), so a test that read the repository's own
/// <c>data</c> folder gave different answers with and without it (#252). Saves go to the sibling <c>saves</c> folder, which
/// keeps the repository's own <c>saves</c> clean.
/// </summary>
internal static class BridgeTestData
{
    private static readonly Lazy<string> Base = new(Create);

    public static string DataRoot => Path.Combine(Base.Value, "data");

    public static string SavesDirectory => Path.Combine(Base.Value, "saves");

    private static string Create()
    {
        var root = Directory.CreateTempSubdirectory("paddock-bridge-tests-").FullName;
        Copy(Path.Combine(BridgeHost.RepositoryRoot(), "data", "authored"), Path.Combine(root, "data", "authored"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        };
        return root;
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
