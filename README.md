# LegacyForgePatcher

[![CI](https://img.shields.io/github/actions/workflow/status/void-community/LegacyForgePatcher/runnable-commit.yml?branch=main&style=for-the-badge)](https://github.com/void-community/LegacyForgePatcher/actions/workflows/runnable-commit.yml)
[![NuGet](https://img.shields.io/nuget/v/LegacyForgePatcher?style=for-the-badge)](https://www.nuget.org/packages/LegacyForgePatcher)
[![License](https://img.shields.io/github/license/void-community/LegacyForgePatcher?style=for-the-badge)](LICENSE)

**Run Forge for Minecraft 1.7.2 on Java 8u20 and later.**

Forge for Minecraft 1.7.2 can't start on current Java 8 releases. Servers and clients stop before Minecraft loads:

```text
[main/INFO] [LaunchWrapper]: Calling tweak class cpw.mods.fml.common.launcher.FMLInjectionAndSortingTweaker
[main/ERROR] [LaunchWrapper]: Unable to launch
java.util.ConcurrentModificationException
	at java.util.ArrayList$Itr.checkForComodification(ArrayList.java:911) ~[?:1.8.0_504]
	at java.util.ArrayList$Itr.remove(ArrayList.java:875) ~[?:1.8.0_504]
	at net.minecraft.launchwrapper.Launch.launch(Launch.java:117) [launchwrapper-1.9.jar:?]
```

LegacyForgePatcher is a native JVM agent that fixes this while the game loads. It changes no game or Forge files.

## Usage

Download the agent for your platform from the [**NuGet package**](https://www.nuget.org/packages/LegacyForgePatcher). A `.nupkg` is a zip archive with the agent under `runtimes/<platform>/native`:

```sh
curl -fsSL -o LegacyForgePatcher.nupkg https://www.nuget.org/api/v2/package/LegacyForgePatcher
unzip -j LegacyForgePatcher.nupkg runtimes/linux-x64/native/LegacyForgePatcher.so
```

Load it with `-agentpath`, which takes an absolute path, in the Java arguments of the server or the launcher profile:

```sh
java -agentpath:/opt/patcher/LegacyForgePatcher.so -jar forge-1.7.2-10.12.2.1161-mc172-universal.jar nogui
```

With [**itzg/minecraft-server**](https://github.com/itzg/docker-minecraft-server), mount the agent and pass it through `JVM_OPTS`:

```sh
docker run -d -e EULA=TRUE -e TYPE=FORGE -e VERSION=1.7.2 \
  -e JVM_OPTS=-agentpath:/patcher/LegacyForgePatcher.so \
  -v "$PWD/LegacyForgePatcher.so:/patcher/LegacyForgePatcher.so:ro" \
  itzg/minecraft-server:java8
```

The game's log confirms the fix:

```text
LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager to sort tweakers without disturbing their iteration
```

.NET applications and test projects can reference the package instead: their build output gets the agents under `runtimes/<platform>/native`.

## Compatibility

| Platform      | Package path                                     |
|---------------|--------------------------------------------------|
| Linux x64     | `runtimes/linux-x64/native/LegacyForgePatcher.so`   |
| Linux ARM64   | `runtimes/linux-arm64/native/LegacyForgePatcher.so` |

CI starts a Forge 1.7.2 server in itzg/minecraft-server with the agent on both platforms for every commit. The agent leaves a game alone when its Forge doesn't sort this way; Forge for Minecraft 1.6.4, for example, starts on Java 8 without it.

## How it works

FML for Minecraft 1.7.2 sorts LaunchWrapper's list of tweakers with `Collections.sort` from `CoreModManager.sortTweakList`, while LaunchWrapper is still iterating that list. Before 8u20, Java sorted a list by setting its elements back in place, which iterators don't notice. Since 8u20 ([**JDK-8032636**](https://bugs.openjdk.org/browse/JDK-8032636)), `Collections.sort` calls `ArrayList.sort`, which counts as a structural change, so LaunchWrapper's next step throws.

The agent is written in C# with [**JvmBridge**](https://github.com/void-community/JvmBridge) and compiled with NativeAOT, so it needs no .NET installation. When the JVM starts, it defines a class whose `sort` sorts a copy of the list and sets the elements back, as Java did before 8u20 and as [**FML for Minecraft 1.7.10**](https://github.com/MinecraftForge/MinecraftForge/blob/1.7.10/fml/src/main/java/cpw/mods/fml/relauncher/CoreModManager.java) later did itself. When FML's `CoreModManager` loads, the agent points its `Collections.sort` calls at that class. Nothing else in the game changes.

## Building

Install the .NET 10 SDK, Clang and zlib development headers, then publish the agent for each platform:

```sh
dotnet publish src/LegacyForgePatcher -c Release -r linux-x64 -o artifacts/agent/linux-x64
```

`dotnet test LegacyForgePatcher.slnx` runs the unit tests and skips the native ones. To run those, set `TARGET_RID` to a published platform and `JAVA_HOME` to a Java 8 JDK; the Forge server test also needs Docker.

## Packages

Every validated commit to `main` publishes its tested NuGet package using Trusted Publishing. Versions follow [**NetAgents' timestamp scheme**](https://github.com/caunt/NetAgents): `YY.M.D.B`, with a UTC time-of-day bucket from 1000 through 9999. There are no release PRs or tags.

## License

[**MIT**](LICENSE)
