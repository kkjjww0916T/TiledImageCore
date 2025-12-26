namespace PartMaker.Client.Types;

/// <summary>
/// PartMaker CLI exit codes.
/// </summary>
public enum ExitCode
{
    /// <summary>Operation completed successfully.</summary>
    Success = 0,

    /// <summary>Operation was cancelled by user.</summary>
    Cancelled = 1,

    /// <summary>Invalid command line arguments.</summary>
    InvalidArgs = 2,

    /// <summary>Specified file was not found.</summary>
    FileNotFound = 3,

    /// <summary>Part already exists in library.</summary>
    AlreadyExists = 4,

    /// <summary>Part was not found in library.</summary>
    PartNotFound = 5,

    /// <summary>No changes detected.</summary>
    NoChanges = 6,

    /// <summary>Invalid version format.</summary>
    InvalidVersion = 7,

    /// <summary>Internal error occurred.</summary>
    InternalError = 10
}
