namespace Paddock.Persistence;

/// <summary>
/// The file was written by a newer build. Opening it is refused so the file is left unchanged.
/// </summary>
public sealed class SaveSchemaTooNewException : Exception
{
    public SaveSchemaTooNewException(string path, int fileSchemaVersion, int supportedSchemaVersion)
        : base(
            $"Save '{path}' uses schema version {fileSchemaVersion}, but this build supports version {supportedSchemaVersion}.")
    {
        Path = path;
        FileSchemaVersion = fileSchemaVersion;
        SupportedSchemaVersion = supportedSchemaVersion;
    }

    public string Path { get; }

    public int FileSchemaVersion { get; }

    public int SupportedSchemaVersion { get; }
}
