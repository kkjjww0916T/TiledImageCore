namespace PartMaker.Client.Options;

/// <summary>
/// Options for the update command.
/// </summary>
/// <param name="LibraryPath">Path to the library directory.</param>
/// <param name="XmlContent">Modified Part XML content.</param>
/// <param name="DryRun">Only check changes without applying.</param>
public record UpdateOptions(
    string LibraryPath,
    string XmlContent,
    bool DryRun = false);
