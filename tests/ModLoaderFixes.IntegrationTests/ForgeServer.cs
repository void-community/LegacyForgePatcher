using System.Globalization;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>Starts Forge servers as itzg installs them, with the agent loaded, and logs players in to them.</summary>
internal static class ForgeServer
{
    /// <summary>
    /// The xUnit collection of the server tests, which xUnit runs one at a time: each installs its server from Mojang's and Forge's servers, and installs that run side by
    /// side fail their downloads more often.
    /// </summary>
    internal const string Collection = "Forge servers";

    /// <summary>The line the agent logs when it makes the class hierarchy cache of ModLauncher 2 to 5, which Forge for Minecraft 1.13.2 to 1.15.2 ships, safe to share.</summary>
    internal const string ModLauncherPatched =
        "ModLoaderFixes: patched cpw/mods/modlauncher/TransformerClassWriter (<clinit>()V: 1 new java/util/HashMap() replaced by modloaderfixes/ClassHierarchyCache.parents() stored in classParents";

    /// <summary>The line a server logs when it is ready for players.</summary>
    internal const string ReadyLine = "Done (";

    private const string AgentDirectory = "/patcher/";
    private const int InstallAttempts = 3;
    private const string LoaderDirectory = "/loader/";

    /// <summary>Gets how long installing and starting a server may take.</summary>
    internal static TimeSpan StartTimeout { get; } = TimeSpan.FromMinutes(minutes: 15);

    /// <summary>Checks that the server's log has a line, and shows the end of the log when it hasn't: the start, which is all a failed Contains prints, is itzg's setup.</summary>
    /// <param name="logs">The server's log.</param>
    /// <param name="expected">The text a line must have.</param>
    internal static void AssertLogged(string logs, string expected)
    {
        Assert.True(
            logs.Contains(expected, StringComparison.Ordinal),
            $"The server didn't log \"{expected}\". Its log ends with:{Environment.NewLine}{Tail(logs)}"
        );
    }

    /// <summary>Checks that no line of the server's log has a text, and shows the lines that have it when one does.</summary>
    /// <param name="logs">The server's log.</param>
    /// <param name="unexpected">The text no line may have.</param>
    internal static void AssertNotLogged(string logs, string unexpected)
    {
        string[] lines = [.. logs.Split(separator: '\n').Where(line => line.Contains(unexpected, StringComparison.Ordinal))];

        Assert.True(
            lines.Length == 0,
            $"The server logged \"{unexpected}\":{Environment.NewLine}{string.Join(Environment.NewLine, lines.Take(count: 10))}"
        );
    }

    /// <summary>Compiles the login client for Java 8, which every server image runs.</summary>
    /// <param name="cancellationToken">Stops the compilation.</param>
    /// <returns>The directory of its classes.</returns>
    internal static Task<string> CompileLoginLoadAsync(CancellationToken cancellationToken)
    {
        return new Jdk(major: 8).CompileAsync(sources: "Fixtures/LoginLoad/**/*.java", cancellationToken: cancellationToken);
    }

    /// <summary>Describes a server of the given loader and version that loads the agent with <paramref name="option"/>.</summary>
    /// <param name="image">The itzg/minecraft-server image, which decides the Java version.</param>
    /// <param name="type">itzg's TYPE: FORGE or NEOFORGE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="memory">The server's heap.</param>
    /// <param name="option">The JVM option that loads the agent: <c>-agentpath:</c> or <c>-javaagent:</c>.</param>
    /// <param name="agent">The agent's library or jar.</param>
    /// <returns>The container's builder.</returns>
    internal static ContainerBuilder Create(string image, string type, string version, string memory, string option, FileInfo agent)
    {
        return new ContainerBuilder(image)
            .WithEnvironment(name: "EULA", value: "TRUE")
            .WithEnvironment(name: "TYPE", type)
            .WithEnvironment(name: "VERSION", version)
            .WithEnvironment(name: "MEMORY", memory)
            .WithEnvironment(name: "JVM_OPTS", option + AgentDirectory + agent.Name)
            .WithResourceMapping(agent, AgentDirectory);
    }

    /// <summary>
    /// Logs 200 players in, 40 at a time, from inside the server's container, so the logins load its event loops without depending on the host's port forwarding.
    /// Forge logs its ready line before it admits players, so one player logs in first, until the server lets it, and the 200 follow.
    /// </summary>
    /// <param name="server">The started server.</param>
    /// <param name="protocol">The protocol version of the server's Minecraft.</param>
    /// <param name="fmlVersion">FML's network version, to log in as a Forge client, or <see langword="null"/> to log in as a vanilla one.</param>
    /// <param name="cancellationToken">Stops the logins.</param>
    /// <returns>The login client's result; it prints <c>LOGINS {logged in=200}</c> and exits with 0 when every login reached the play state.</returns>
    internal static async Task<ExecResult> LogInPlayersAsync(IContainer server, int protocol, string? fmlVersion, CancellationToken cancellationToken)
    {
        List<string> command = ["java", "-cp", LoaderDirectory, "loginload.LoginLoad", "127.0.0.1", "25565", protocol.ToString(CultureInfo.InvariantCulture), "200", "40"];

        if (fmlVersion is not null)
            command.Add(fmlVersion);

        try
        {
            return await server.ExecAsync(command, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw await TimedOutAsync(server, phase: "The logins didn't finish", exception).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Reads the server's log, both streams.</summary>
    /// <param name="server">The container whose output is the server's log.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The log.</returns>
    internal static async Task<string> ReadLogsAsync(IContainer server, CancellationToken cancellationToken)
    {
        (string standardOutput, string standardError) = await server.GetLogsAsync(ct: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return standardOutput + standardError;
    }

    /// <summary>
    /// Starts the server and waits until it is ready or has exited. itzg installs the loader first, and the installer downloads from Mojang's and Forge's servers; when
    /// a download fails the install does too, and the container is started again, up to <see cref="InstallAttempts"/> times, as itzg installs again on a new start.
    /// </summary>
    /// <param name="server">The container that installs and runs the server.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The server's log so far, across its starts.</returns>
    internal static async Task<string> StartAsync(IContainer server, CancellationToken cancellationToken)
    {
        try
        {
            return await StartUntilReadyAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw await TimedOutAsync(server, phase: "The server wasn't ready", exception).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Lets players in without accounts, as many as a burst of logins brings, and puts the login client beside the server.</summary>
    /// <param name="server">The server's builder.</param>
    /// <param name="loginLoad">The compiled login client, from <see cref="CompileLoginLoadAsync"/>.</param>
    /// <returns>The builder.</returns>
    internal static ContainerBuilder WithLoginLoad(this ContainerBuilder server, string loginLoad)
    {
        return server
            .WithEnvironment(name: "ONLINE_MODE", value: "FALSE")
            .WithEnvironment(name: "MAX_PLAYERS", value: "1000")
            .WithResourceMapping(new DirectoryInfo(loginLoad), LoaderDirectory);
    }

    // itzg logs "[init] [ERROR] Failed to install Forge" (or NeoForge) once per failed install.
    private static int CountInstallFailures(string logs)
    {
        return logs.Split(separator: '\n').Count(static line => line.Contains(value: "[ERROR] Failed to install", StringComparison.Ordinal));
    }

    private static async Task<string> StartUntilReadyAsync(IContainer server, CancellationToken cancellationToken)
    {
        string logs = string.Empty;

        for (int attempt = 1; attempt <= InstallAttempts; attempt++)
        {
            await server.StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Task<long> exit = server.GetExitCodeAsync(cancellationToken);

            logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            while (!logs.Contains(ReadyLine, StringComparison.Ordinal) && !exit.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds: 2), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }

            if (logs.Contains(ReadyLine, StringComparison.Ordinal) || CountInstallFailures(logs) < attempt)
                return logs;
        }

        return logs;
    }

    private static string Tail(string logs)
    {
        string[] lines = logs.Split(separator: '\n');

        return string.Join(separator: '\n', lines.Skip(Math.Max(val1: 0, lines.Length - 40)));
    }

    // A timed-out wait would otherwise report only that a task was canceled; the end of the log says whether the install, the start or the logins took the time.
    private static async Task<TimeoutException> TimedOutAsync(IContainer server, string phase, OperationCanceledException exception)
    {
        string logs = await ReadLogsAsync(server, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        return new TimeoutException($"{phase} within {StartTimeout.TotalMinutes} minutes. The server's log ends with:{Environment.NewLine}{Tail(logs)}", exception);
    }
}
