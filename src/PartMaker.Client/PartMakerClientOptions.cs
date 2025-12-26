namespace PartMaker.Client;

/// <summary>
/// Configuration options for PartMakerClient.
/// </summary>
public class PartMakerClientOptions
{
    /// <summary>Path to PartMaker.exe.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Working directory for CLI execution.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Enable debug mode (adds --debug flag).</summary>
    public bool DebugMode { get; init; } = false;

    /// <summary>Timeout for CLI operations in milliseconds. Default: 60000 (1 minute).</summary>
    public int TimeoutMs { get; init; } = 60000;
}
