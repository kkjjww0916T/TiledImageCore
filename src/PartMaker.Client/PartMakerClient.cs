using System.Diagnostics;
using PartMaker.Client.Options;
using PartMaker.Client.Types;

namespace PartMaker.Client;

/// <summary>
/// Default implementation of IPartMakerClient.
/// Wraps PartMaker CLI commands with type-safe API.
/// </summary>
public class PartMakerClient : IPartMakerClient
{
    private readonly PartMakerClientOptions _options;
    private bool _disposed;

    public PartMakerClient(PartMakerClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ExecutablePath);

        _options = options;
    }

    public async Task<PartMakerResult<string>> CreateAsync(
        CreateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = $"create --library \"{options.LibraryPath}\" --image \"{options.ImagePath}\"";
        return await ExecuteAsync(args, stdinInput: null, cancellationToken);
    }

    public async Task<PartMakerResult<string>> EditAsync(
        EditOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = $"edit --library \"{options.LibraryPath}\"";
        if (!string.IsNullOrEmpty(options.ImagePath))
            args += $" --image \"{options.ImagePath}\"";

        return await ExecuteAsync(args, options.XmlContent, cancellationToken);
    }

    public async Task<PartMakerResult<string>> RegisterAsync(
        RegisterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = $"register --library \"{options.LibraryPath}\" --image \"{options.ImagePath}\"";
        if (options.Force)
            args += " --force";

        return await ExecuteAsync(args, options.XmlContent, cancellationToken);
    }

    public async Task<PartMakerResult<string>> UpdateAsync(
        UpdateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = $"update --library \"{options.LibraryPath}\"";
        if (options.DryRun)
            args += " --dry-run";

        return await ExecuteAsync(args, options.XmlContent, cancellationToken);
    }

    public async Task<PartMakerResult<string>> SelectAsync(
        SelectOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = $"select --library \"{options.LibraryPath}\"";
        return await ExecuteAsync(args, stdinInput: null, cancellationToken);
    }

    private async Task<PartMakerResult<string>> ExecuteAsync(
        string arguments,
        string? stdinInput,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_options.DebugMode)
            arguments += " --debug";

        var psi = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            Arguments = arguments,
            RedirectStandardInput = stdinInput != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
                return PartMakerResult<string>.Failure(ExitCode.InternalError, "Failed to start process");

            if (stdinInput != null)
            {
                await process.StandardInput.WriteAsync(stdinInput);
                process.StandardInput.Close();
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.TimeoutMs);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            var output = await outputTask;
            var error = await errorTask;

            var exitCode = (ExitCode)process.ExitCode;

            return exitCode == ExitCode.Success
                ? PartMakerResult<string>.Success(output.Trim())
                : exitCode == ExitCode.Cancelled
                    ? PartMakerResult<string>.Cancelled()
                    : PartMakerResult<string>.Failure(exitCode, error.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PartMakerResult<string>.Failure(ExitCode.InternalError, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
