namespace PartMaker.Client.Options;

/// <summary>
/// Options for the edit command.
/// </summary>
/// <param name="LibraryPath">Path to the library directory.</param>
/// <param name="XmlContent">Part XML content to edit.</param>
/// <param name="ImagePath">Optional path to the source image file.</param>
public record EditOptions(string LibraryPath, string XmlContent, string? ImagePath = null);
