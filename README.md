# 🔧 LegacyForgePatcher

[![CI](https://img.shields.io/github/actions/workflow/status/void-community/LegacyForgePatcher/runnable-commit.yml?branch=main&style=for-the-badge)](https://github.com/void-community/LegacyForgePatcher/actions/workflows/runnable-commit.yml)
[![Release](https://img.shields.io/github/v/release/void-community/LegacyForgePatcher?style=for-the-badge)](https://github.com/void-community/LegacyForgePatcher/releases/latest)
[![License](https://img.shields.io/github/license/void-community/LegacyForgePatcher?style=for-the-badge)](LICENSE)

**Fix Forge servers and clients that break on current Java, under load or behind a proxy.** ☕

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

**Forge for Minecraft 1.13 to 1.20.1, and NeoForge for 1.20.1, drop players at login when the server is busy:**

```text
[Server thread/INFO] [minecraft/ServerLoginNetHandler]: com.mojang.authlib.GameProfile@6ac532df[id=<null>,name=Player,properties={},legacy=false] (/172.17.0.1:43658) lost connection: Internal Exception: java.util.ConcurrentModificationException
```

**Forge and NeoForge clients from Minecraft 1.20.1 sometimes crash before the game's window opens:**

```text
java.lang.RuntimeException: java.util.concurrent.ExecutionException: java.lang.NullPointerException: Cannot invoke "java.lang.Boolean.booleanValue()" because the return value of "net.minecraftforge.fml.loading.FMLConfig$ConfigValue.getConfigValue(com.electronwill.nightconfig.core.file.CommentedFileConfig)" is null
	at net.minecraftforge.fml.earlydisplay.DisplayWindow.setupMinecraftWindow(DisplayWindow.java:527)
	at net.minecraftforge.fml.loading.ImmediateWindowHandler.setupMinecraftWindow(ImmediateWindowHandler.java:54)
```

**Forge for Minecraft 1.12 and 1.12.1 clients crash when a proxy such as BungeeCord or Void moves them to another server:**

```text
[Netty Epoll Client IO #1] [INFO] Injecting existing registry data into this client instance
[main] [FATAL] net.minecraft.util.ReportedException: Ticking entity
	at net.minecraft.world.World.func_72939_s(World.java:1766)
	at net.minecraft.client.Minecraft.func_71407_l(Minecraft.java:1841)
Caused by: java.lang.NullPointerException
	at net.minecraft.entity.player.EntityPlayer.func_192028_j(EntityPlayer.java:589)
	at net.minecraft.entity.player.EntityPlayer.func_70636_d(EntityPlayer.java:574)
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

✅ The game's log confirms each fix when Forge loads the code it repairs; on Forge 1.12 that is at the first connection:

```text
LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager (sortTweakList()V: 1 invocation of java/util/Collections.sort redirected)
LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher (serverInitiateHandshake()I: 1 invocation of net/minecraft/network/NetworkManager.func_150723_a redirected)
LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4 (accept: 1 invocation of net/minecraftforge/registries/GameData.injectSnapshot redirected)
LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7 (accept: 1 invocation of net/minecraftforge/registries/GameData.revertToFrozen redirected)
LegacyForgePatcher: patched net/minecraftforge/network/HandshakeHandler (<init>: sentMessages created as a CopyOnWriteArrayList (1 change))
LegacyForgePatcher: patched net/minecraftforge/fml/loading/FMLConfig (load()V: 1 invocation of net/minecraftforge/fml/loading/FMLConfig.loadFrom redirected)
```

## 📦 Compatibility

| Fix                                   | Side   | Minecraft             | Forge                                      |
|---------------------------------------|--------|-----------------------|--------------------------------------------|
| Launches on Java 8u20 and later       | Server | 1.7.2                 | 10.12.2.1161                               |
| Keeps players that log in under load  | Server | 1.12, 1.12.1, 1.12.2  | 14.21.1.2387, 14.22.1.2478, 14.23.5.2859   |
| Survives a proxy's server switch      | Client | 1.12, 1.12.1          | 14.21.1.2387, 14.22.1.2478                 |
| Keeps players that log in under load  | Server | 1.13.2, 1.14.4, 1.15, 1.16.5, 1.17.1, 1.18.2, 1.19.2, 1.20.1 | 25.0.223, 28.2.26, 29.0.4, 36.2.39, 37.1.1, 40.2.0, 43.5.0, 47.4.28; NeoForge 47.1.106 |
| Opens the game's window at startup    | Client | 1.20.1 to 1.21.4      | 47.4.28, 48.1.0, 49.2.0, 50.2.0, 52.1.0, 54.1.0; NeoForge 20.2 to 21.4 (FancyModLoader 1.0 to 6.0) |

| Platform    | `-javaagent` jar         | `-agentpath` library                |
|-------------|--------------------------|-------------------------------------|
| Linux x64   | `legacyforgepatcher.jar` | `legacyforgepatcher-linux-x64.so`   |
| Linux ARM64 | `legacyforgepatcher.jar` | `legacyforgepatcher-linux-arm64.so` |

The agent needs glibc 2.34 or later, as on Ubuntu 22.04, Debian 12 and RHEL 9. itzg's `java8` and `java21` images have it; its `java16` and `java17` images are built on Ubuntu 20.04, whose glibc 2.31 can't load the agent yet.

The jar unpacks the library of its platform into a temporary directory when the JVM starts. To load the library directly, pass the `.so` of your platform to `-agentpath` with an absolute path.

For every commit, CI starts these Forge servers on Java 8 in itzg/minecraft-server with the agent on both platforms: Forge 1.7.2 once with the jar and once with the `.so`, each Forge 1.12 build with the jar and 200 players logging in at once, and each Forge build from 1.13.2 to 1.20.1, and NeoForge for 1.20.1, with the jar and 200 players logging in at once through FML's handshake. Forge 1.19.4, 1.20.2 and 1.21.4, and NeoForge 20.4.253 and 21.4.161, start with the agent loading FML's config; the loading window that crashes only exists on clients, so that fix runs against a fixture of FML's two loads, on Java 17 and 21. The client fix runs against a fixture that drives FML's handshake as those Forge builds do, under the JVM's strictest verifier; Forge for 1.12.2 makes the same change itself, so the agent leaves its client alone. The agent leaves a game alone when its Forge doesn't have the code a fix targets; Forge for Minecraft 1.6.4, for example, starts on Java 8 without it.

## 📄 License

[**MIT**](LICENSE)
