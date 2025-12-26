namespace PartMaker.Client.Options;

/// <summary>
/// Options for the register command.
/// </summary>
/// <param name="LibraryPath">Path to the library directory.</param>
/// <param name="XmlContent">Part XML content to register.</param>
/// <param name="ImagePath">Path to the source image file.</param>
/// <param name="Force">Force overwrite if already exists.</param>
public record RegisterOptions(
    string LibraryPath,
    string XmlContent,
    string ImagePath,
    bool Force = false);
