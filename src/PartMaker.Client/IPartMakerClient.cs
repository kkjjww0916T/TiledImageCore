using PartMaker.Client.Options;
using PartMaker.Client.Types;

namespace PartMaker.Client;

/// <summary>
/// Client interface for PartMaker CLI operations.
/// </summary>
public interface IPartMakerClient : IDisposable
{
    /// <summary>
    /// Create a new Part with the specified image.
    /// Opens PartMaker dialog for user to define regions.
    /// </summary>
    /// <param name="options">Create options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the Part XML content.</returns>
    Task<PartMakerResult<string>> CreateAsync(
        CreateOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edit an existing Part XML.
    /// Opens PartMaker dialog for user to modify regions.
    /// </summary>
    /// <param name="options">Edit options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the modified Part XML content.</returns>
    Task<PartMakerResult<string>> EditAsync(
        EditOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a Part to the library.
    /// Creates version 1.0.0.0 from a 0.0.0.0 Part.
    /// </summary>
    /// <param name="options">Register options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the registered Part file path.</returns>
    Task<PartMakerResult<string>> RegisterAsync(
        RegisterOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing Part in the library.
    /// </summary>
    /// <param name="options">Update options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the update report (JSON) or updated path.</returns>
    Task<PartMakerResult<string>> UpdateAsync(
        UpdateOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Open Part selection dialog.
    /// </summary>
    /// <param name="options">Select options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the selected Part XML file path.</returns>
    Task<PartMakerResult<string>> SelectAsync(
        SelectOptions options,
        CancellationToken cancellationToken = default);
}
