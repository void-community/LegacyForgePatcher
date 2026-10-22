# 🔧 LegacyForgePatcher

[![CI](https://img.shields.io/github/actions/workflow/status/void-community/LegacyForgePatcher/runnable-commit.yml?branch=main&style=for-the-badge)](https://github.com/void-community/LegacyForgePatcher/actions/workflows/runnable-commit.yml)
[![Release](https://img.shields.io/github/v/release/void-community/LegacyForgePatcher?style=for-the-badge)](https://github.com/void-community/LegacyForgePatcher/releases/latest)
[![License](https://img.shields.io/github/license/void-community/LegacyForgePatcher?style=for-the-badge)](LICENSE)

**Fix legacy Forge servers that break on current Java or under load.** ☕

LegacyForgePatcher is a native Java agent that repairs these while the game loads. It changes no game or Forge files and needs no .NET installation. ✨

## 🩹 What it fixes

**Forge for Minecraft 1.7.2 stops before Minecraft loads on Java 8u20 and later:**

```text
[main/INFO] [LaunchWrapper]: Calling tweak class cpw.mods.fml.common.launcher.FMLInjectionAndSortingTweaker
[main/ERROR] [LaunchWrapper]: Unable to launch
java.util.ConcurrentModificationException
	at java.util.ArrayList$Itr.checkForComodification(ArrayList.java:911) ~[?:1.8.0_504]
	at java.util.ArrayList$Itr.remove(ArrayList.java:875) ~[?:1.8.0_504]
	at net.minecraft.launchwrapper.Launch.launch(Launch.java:117) [launchwrapper-1.9.jar:?]
```

**Forge for Minecraft 1.12 drops players at login when the server is busy:**

```text
[Server thread/INFO] [minecraft/NetHandlerPlayServer]: Player lost connection: Internal Exception: io.netty.handler.codec.EncoderException: java.io.IOException: Can't serialize unregistered packet
```

## 🚀 Usage

Download the agent from the [**latest release**](https://github.com/void-community/LegacyForgePatcher/releases/latest). One jar serves Linux x64 and ARM64:

```sh
curl -fsSL -O https://github.com/void-community/LegacyForgePatcher/releases/latest/download/legacyforgepatcher.jar
```

Add it to the Java arguments of the server or the launcher profile with `-javaagent`:

```sh
java -javaagent:legacyforgepatcher.jar -jar forge-1.12.2-14.23.5.2859.jar nogui
```

🐳 With [**itzg/minecraft-server**](https://github.com/itzg/docker-minecraft-server), mount the jar and pass it through `JVM_OPTS`:

```sh
docker run -d -e EULA=TRUE -e TYPE=FORGE -e VERSION=1.12.2 \
  -e JVM_OPTS=-javaagent:/patcher/legacyforgepatcher.jar \
  -v "$PWD/legacyforgepatcher.jar:/patcher/legacyforgepatcher.jar:ro" \
  itzg/minecraft-server:java8
```

✅ The game's log confirms each fix when Forge loads the code it repairs; on Forge 1.12 that is at the first login:

```text
LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager (sortTweakList()V: 1 invocation of java/util/Collections.sort redirected)
LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher (serverInitiateHandshake()I: 1 invocation of net/minecraft/network/NetworkManager.func_150723_a redirected)
```

## 📦 Compatibility

| Minecraft | Forge         | Fix                                    |
|-----------|---------------|----------------------------------------|
| 1.7.2     | 10.12.2.1161  | Launches on Java 8u20 and later        |
| 1.12      | 14.21.1.2387  | Keeps players that log in under load   |
| 1.12.1    | 14.22.1.2478  | Keeps players that log in under load   |
| 1.12.2    | 14.23.5.2859  | Keeps players that log in under load   |

| Platform    | `-javaagent` jar         | `-agentpath` library                |
|-------------|--------------------------|-------------------------------------|
| Linux x64   | `legacyforgepatcher.jar` | `legacyforgepatcher-linux-x64.so`   |
| Linux ARM64 | `legacyforgepatcher.jar` | `legacyforgepatcher-linux-arm64.so` |

The jar unpacks the library of its platform into a temporary directory when the JVM starts. To load the library directly, pass the `.so` of your platform to `-agentpath` with an absolute path.

For every commit, CI starts these Forge servers on Java 8 in itzg/minecraft-server with the agent on both platforms: Forge 1.7.2 once with the jar and once with the `.so`, and each Forge 1.12 build with the jar and 200 players logging in at once. The agent leaves a game alone when its Forge doesn't have the code a fix targets; Forge for Minecraft 1.6.4, for example, starts on Java 8 without it.

## 📄 License

[**MIT**](LICENSE)
