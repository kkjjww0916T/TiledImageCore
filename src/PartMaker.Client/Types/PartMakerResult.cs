namespace PartMaker.Client.Types;

/// <summary>
/// Result of a PartMaker CLI operation.
/// </summary>
/// <typeparam name="T">Type of the result value.</typeparam>
public readonly struct PartMakerResult<T>
{
    /// <summary>Exit code from the CLI.</summary>
    public ExitCode ExitCode { get; }

    /// <summary>Result value on success.</summary>
    public T? Value { get; }

    /// <summary>Error message on failure.</summary>
    public string? Error { get; }

    /// <summary>Whether the operation was successful.</summary>
    public bool IsSuccess => ExitCode == ExitCode.Success;

    /// <summary>Whether the operation was cancelled by user.</summary>
    public bool IsCancelled => ExitCode == ExitCode.Cancelled;

    private PartMakerResult(ExitCode exitCode, T? value, string? error)
    {
        ExitCode = exitCode;
        Value = value;
        Error = error;
    }

    /// <summary>Creates a successful result.</summary>
    public static PartMakerResult<T> Success(T value)
        => new(ExitCode.Success, value, null);

    /// <summary>Creates a failure result.</summary>
    public static PartMakerResult<T> Failure(ExitCode exitCode, string? error)
        => new(exitCode, default, error);

    /// <summary>Creates a cancelled result.</summary>
    public static PartMakerResult<T> Cancelled()
        => new(ExitCode.Cancelled, default, null);
}
