using System.Diagnostics;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The JDK that <c>JAVA_HOME</c> names, which compiles and runs the Java fixtures.</summary>
internal static class Jdk
{
    /// <summary>Compiles the fixtures into a new directory and returns it as a class path.</summary>
    public static async Task<string> CompileFixturesAsync(CancellationToken cancellationToken)
    {
        string classes = Directory.CreateTempSubdirectory(prefix: "legacyforgepatcher-").FullName;
        string[] sources = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, path2: "Fixtures"), searchPattern: "*.java", SearchOption.AllDirectories);
        JdkResult result = await RunAsync(tool: "javac", ["-d", classes, .. sources], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return result.ExitCode is not 0
            ? throw new InvalidOperationException($"javac failed with exit code {result.ExitCode}:\n{result.Output}")
            : classes;
    }

    /// <summary>Runs a JDK tool and returns its exit code with its standard output and error.</summary>
    public static async Task<JdkResult> RunAsync(string tool, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        string javaHome = Environment.GetEnvironmentVariable(variable: "JAVA_HOME") ?? throw new InvalidOperationException(message: "Set JAVA_HOME to the JDK to test with.");

        ProcessStartInfo launch = new(Path.Combine(javaHome, path2: "bin", tool))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
            launch.ArgumentList.Add(argument);

        using Process process = Process.Start(launch) ?? throw new InvalidOperationException($"{tool} didn't start.");

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        string[] streams = await Task.WhenAll(output, error).ConfigureAwait(continueOnCapturedContext: false);

        return new JdkResult(process.ExitCode, string.Concat(streams));
    }
}
