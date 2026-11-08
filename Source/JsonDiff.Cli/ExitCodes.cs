namespace JsonDiff.Cli;

/// <summary>Process exit codes, following <c>diff</c> so scripts can branch on the result.</summary>
public static class ExitCodes
{
    public const int Identical = 0;
    public const int Different = 1;

    /// <summary>Invalid arguments, an unreadable file, invalid JSON or invalid match rules.</summary>
    public const int Error = 2;
}
