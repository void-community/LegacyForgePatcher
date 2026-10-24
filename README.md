# 🔧 ModLoaderFixes

[![CI](https://img.shields.io/github/actions/workflow/status/void-community/ModLoaderFixes/runnable-commit.yml?branch=main&style=for-the-badge)](https://github.com/void-community/ModLoaderFixes/actions/workflows/runnable-commit.yml)
[![Release](https://img.shields.io/github/v/release/void-community/ModLoaderFixes?style=for-the-badge)](https://github.com/void-community/ModLoaderFixes/releases/latest)
[![License](https://img.shields.io/github/license/void-community/ModLoaderFixes?style=for-the-badge)](LICENSE)

**Fix Forge, NeoForge and Minecraft servers and clients that break on current Java, under load or behind a proxy.** ☕

A native Java agent that repairs these as the game loads. It changes no game files and needs no .NET. ✨

## 🩹 What it fixes

**Forge 1.7.2 servers stop before Minecraft loads on Java 8u20 and later:**

```text
[main/INFO] [LaunchWrapper]: Calling tweak class cpw.mods.fml.common.launcher.FMLInjectionAndSortingTweaker
[main/ERROR] [LaunchWrapper]: Unable to launch
java.util.ConcurrentModificationException
	at java.util.ArrayList$Itr.checkForComodification(ArrayList.java:911) ~[?:1.8.0_504]
	at java.util.ArrayList$Itr.remove(ArrayList.java:875) ~[?:1.8.0_504]
	at net.minecraft.launchwrapper.Launch.launch(Launch.java:117) [launchwrapper-1.9.jar:?]
```

**Forge 1.7.2 and 1.7.10 servers now and then never answer a login**, and the client waits until it gives up:

```text
[Netty IO #0/DEBUG] [net.minecraft.network.NetworkManager]: Set listener of net.minecraft.network.NetworkManager@1ab07dd4 to net.minecraft.server.network.NetHandlerLoginServer@2bcfb381
[Netty IO #0/DEBUG] [net.minecraft.util.MessageDeserializer]:  IN: [LOGIN:0] net.minecraft.network.login.client.C00PacketLoginStart[]
```

**Forge 1.12 servers drop players at login under load:**

```text
[Server thread/INFO] [minecraft/NetHandlerPlayServer]: Player lost connection: Internal Exception: io.netty.handler.codec.EncoderException: java.io.IOException: Can't serialize unregistered packet
```

**Forge 1.12 and 1.12.1 clients crash when a proxy such as BungeeCord or Void moves them to another server:**

```text
[Netty Epoll Client IO #1] [INFO] Injecting existing registry data into this client instance
[main] [FATAL] net.minecraft.util.ReportedException: Ticking entity
	at net.minecraft.world.World.func_72939_s(World.java:1766)
	at net.minecraft.client.Minecraft.func_71407_l(Minecraft.java:1841)
Caused by: java.lang.NullPointerException
	at net.minecraft.entity.player.EntityPlayer.func_192028_j(EntityPlayer.java:589)
	at net.minecraft.entity.player.EntityPlayer.func_70636_d(EntityPlayer.java:574)
```

**Forge 1.13.2 to 1.20.1 and NeoForge 1.20.1 servers drop players at login under load:**

```text
[Server thread/INFO] [minecraft/ServerLoginNetHandler]: com.mojang.authlib.GameProfile@6ac532df[id=<null>,name=Player,properties={},legacy=false] (/172.17.0.1:43658) lost connection: Internal Exception: java.util.ConcurrentModificationException
```

**Forge 1.13.2 to 1.15.2 now and then crashes while it loads a world or its mods:**

```text
java.lang.NullPointerException: null
	at cpw.mods.modlauncher.TransformerClassWriter.getCommonSuperClass(TransformerClassWriter.java:102) ~[modlauncher-2.1.5.jar:?] {}
	at org.objectweb.asm.SymbolTable.addMergedType(SymbolTable.java:1209) ~[asm-6.2.jar:?] {}
```

**Forge 1.13.2 to 1.19.4 sometimes fails its first start while it corrects `fml.toml`, or writes keys twice:**

```text
com.electronwill.nightconfig.core.io.ParsingException: Invalid TOML data: entry "[disableOptimizedDFU]" defined twice in its table.
Exception in thread "main" java.util.ConcurrentModificationException
	at com.electronwill.nightconfig.core.ConfigSpec.correct(ConfigSpec.java:655)
	at net.minecraftforge.fml.loading.FMLConfig.loadFrom(FMLConfig.java:54)
```

**Forge 1.13.2 to 1.21.4 and NeoForge 1.20.1 sometimes crash while they load mods:**

```text
java.lang.ClassCastException: class java.util.HashMap$Node cannot be cast to class java.util.HashMap$TreeNode (java.util.HashMap$Node and java.util.HashMap$TreeNode are in module java.base of loader 'bootstrap')
	at java.util.HashSet.add(HashSet.java:221)
	at net.minecraftforge.registries.ObjectHolderRegistry.addHandler(ObjectHolderRegistry.java:64)
```

**Forge 1.14.2 and 1.14.3 clients sometimes hang at startup and never reach their menu.** A thread dump shows the client thread waiting in `EventListenerHelper.getListenerListInternal` while a `modloading-worker` thread holds EventBus's lock.

**Forge and NeoForge 1.20.1 to 1.21.4 clients sometimes crash before the game's window opens:**

```text
java.lang.RuntimeException: java.util.concurrent.ExecutionException: java.lang.NullPointerException: Cannot invoke "java.lang.Boolean.booleanValue()" because the return value of "net.minecraftforge.fml.loading.FMLConfig$ConfigValue.getConfigValue(com.electronwill.nightconfig.core.file.CommentedFileConfig)" is null
	at net.minecraftforge.fml.earlydisplay.DisplayWindow.setupMinecraftWindow(DisplayWindow.java:527)
	at net.minecraftforge.fml.loading.ImmediateWindowHandler.setupMinecraftWindow(ImmediateWindowHandler.java:54)
```

**Forge and NeoForge clients from 1.20.6, and vanilla clients from 26.1, sometimes drop their connection as they join or as a proxy moves them to another server:**

```text
[Render thread] [WARN] Client disconnected with reason: Internal Exception: io.netty.handler.codec.EncoderException: Pipeline has no outbound protocol configured, can't process packet ServerboundChunkBatchReceivedPacket
[Netty Epoll IO #0] [ERROR] io.netty.handler.codec.EncoderException: Sending unknown packet 'serverbound/minecraft:chunk_batch_received'
```

## 🚀 Usage

Download the agent from the [**latest release**](https://github.com/void-community/ModLoaderFixes/releases/latest):

```sh
curl -fsSL -O https://github.com/void-community/ModLoaderFixes/releases/latest/download/modloaderfixes.jar
```

Add it to the Java arguments of the server or the launcher profile:

```sh
java -javaagent:modloaderfixes.jar -jar forge-1.12.2-14.23.5.2859.jar nogui
```

🐳 With [**itzg/minecraft-server**](https://github.com/itzg/docker-minecraft-server):

```sh
docker run -d -e EULA=TRUE -e TYPE=FORGE -e VERSION=1.12.2 \
  -e JVM_OPTS=-javaagent:/patcher/modloaderfixes.jar \
  -v "$PWD/modloaderfixes.jar:/patcher/modloaderfixes.jar:ro" \
  itzg/minecraft-server:java8
```

✅ The log shows a line for each fix as the agent applies it:

```text
ModLoaderFixes: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher (serverInitiateHandshake()I: 1 invocation of net/minecraft/network/NetworkManager.func_150723_a redirected)
```

## 📦 Requirements

- 🐧 Linux x64 or ARM64 with glibc 2.17 or later: CentOS 7, Ubuntu 18.04, Debian 10 and newer, and every itzg image. One jar serves both.
- To skip the jar, pass `modloaderfixes-linux-x64.so` or `modloaderfixes-linux-arm64.so` from the release to `-agentpath` with an absolute path.
- 🧪 CI tests every commit: real Forge and NeoForge servers start with the agent, many with 200 players logging in at once, and the client fixes run against reproductions of their bugs.

## 📄 License

[**MIT**](LICENSE)
