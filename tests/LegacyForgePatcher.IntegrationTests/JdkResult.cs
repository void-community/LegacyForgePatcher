namespace LegacyForgePatcher.IntegrationTests;

/// <summary>How a JDK tool exited and what it wrote.</summary>
/// <param name="ExitCode">The tool's exit code.</param>
/// <param name="Output">The tool's standard output followed by its standard error.</param>
internal sealed record JdkResult(int ExitCode, string Output);
