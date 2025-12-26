namespace PartMaker.Client.Options;

/// <summary>
/// Options for the create command.
/// </summary>
/// <param name="LibraryPath">Path to the library directory.</param>
/// <param name="ImagePath">Path to the source image file.</param>
public record CreateOptions(string LibraryPath, string ImagePath);
