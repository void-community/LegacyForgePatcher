# Repository guidelines

LegacyForgePatcher is a NativeAOT JVM agent, written in C# with JvmBridge, that fixes Forge, NeoForge and Minecraft servers and clients where their own code breaks: on current Java, as Forge 1.7.2's launch does, under load, as the logins of Forge 1.12 and of Forge 1.13 to 1.20.1 do, when a proxy moves a Forge 1.12 client to another server, when the loading window of a Forge or NeoForge client from 1.20.1 reads FML's config while FML loads it again, when mods fill Forge's object holder registry from several threads at once, when a Minecraft client from 1.20.5 answers its first chunk batch before play is set up, when Forge 1.14.2 and 1.14.3 build EventBus listener lists under EventBus's lock, or when Forge 1.13.2 to 1.19.4 reload `fml.toml` from a file watcher while they correct it. Each fix targets the exact upstream code it repairs and leaves every other class unchanged.

## Layout

- `src`: the agent, its class patches and the Java helpers those patches call.
- `tests/LegacyForgePatcher.Tests`: xUnit unit tests of class file patching, referencing the agent project.
- `tests/LegacyForgePatcher.IntegrationTests`: xUnit tests that load the published agent into real JVMs, against Java fixtures (one folder per scenario under `Fixtures`) and real Forge servers.
- `.github`: reusable CI, GitHub releases, dependency maintenance, and repository templates.

## Source conventions

Use four-space C# indentation, descriptive identifiers, underscore-prefixed private fields, and camelCase parameters. Put braces on their own lines and omit braces for single-line statement bodies. Keep method signatures on a single line. Prefer pattern matching and nameof. Async methods use an Async suffix. Never silence nullable warnings with the null-forgiving operator. NetAgents formats sources on build; review what it rewrites.

Don't add `global.json`, NuGet lock files, or other .NET JSON configuration. CI pins the SDK in its workflows.

## Native correctness

- Keep every agent callback non-throwing in effect: patch only classes whose bytes match the expected upstream shape, and leave anything else unchanged.
- The Java helpers compile with `javac --release 8`, the earliest Java with the behavior this agent fixes, so it need not load on anything older.
- A helper that calls the game is plain Java compiled against stand-ins in `src/stubs` (`JavaStubSource` items, never shipped), defined with `ClassLoaderTarget.PatchedClassLoader` in the loader of the class that calls it, and redirected to with the call's own descriptor. A stub member must have the game's exact name and descriptor, checked against the real jars, or the helper fails to link there; a private member a helper needs is opened on its class with `SetFieldAccess` or `SetAccess`, keeping its other flags. Only a helper that names no game type, or one whose game types differ between the versions it serves, lives in the bootstrap loader and uses reflection (`Collections`, `ModLoaderConfigs`).
- Prefer the declarative edits (`RedirectInvocation`, `ReplaceConstruction`, `RemoveInvocation`) to `Edit`, and write and match bytecode with `CodeBuilder`, `Opcodes` and `JavaClassFile.Invokes` instead of byte literals, in patches and tests alike.
- Register patches and helpers from `Configure`. Ordinary JNI must not run in Agent_OnLoad, and JvmBridge defines helpers at VM initialization.
- Keep the agent free of host requirements such as libicu: JVM hosts are often slim images. Log through `AgentContext.Log`, not `Console`.

## Verification

Install the .NET 10 SDK, Clang and zlib development headers. The build compiles the Java helper with the JDK in `JvmBridgeJdkHome` or `JAVA_HOME`, and downloads a pinned JDK when neither names one. Run `dotnet build LegacyForgePatcher.slnx -c Release` and `dotnet test LegacyForgePatcher.slnx -c Release --no-build`, which skips the native tests. Publish the agent with `dotnet publish src -c Release -r <RID> -o artifacts/agent/<RID>`, which writes the library and, through `JvmBridgeAgentJar`, `LegacyForgePatcher.jar` for `java -javaagent` beside it. Then run `dotnet test tests/LegacyForgePatcher.IntegrationTests -c Release` with `TARGET_RID=<RID>`. JvmBridge.Testing downloads a checksum-pinned Java 8 JDK, or uses the one `JVMBRIDGE_JDK_8` names. The Forge server test needs Docker. Record which platforms and Java versions actually executed.

The Linux agents must load on glibc 2.17, because Forge from 1.17 to 1.20.4 runs on itzg's Ubuntu 20.04 image. CI publishes them against .NET's Ubuntu 18.04 sysroot through `void-community/actions/glibc-sysroot` and fails when one asks for a newer glibc. A plain local publish links against the machine's own glibc; to reproduce CI's library, publish with `-p:SysRoot=/crossrootfs/<arch> -p:LinkerFlavor=lld` as JvmBridge's Publishing for Linux guide describes, and check it with `readelf -V`.

CI must account for every configured platform. Never weaken tests or skip source, build or workflow changes to get green checks.

## Commits and releases

Use Conventional Commits with a scope, a gitmoji after the scope, and past-tense subjects for completed work. Allowed types: feat, fix, perf, deps, revert, docs, style, chore, refactor, test, build, ci. Use feat/fix for production source behavior.

Examples: `fix(agent): 🐛 patched FML's tweak sort on Java 8u20 and later`, `ci(test): ✅ started Forge servers with the published agent`.

Pull request descriptions use the repository template. Every validated main commit publishes the agents it tested as a GitHub release through `publish-github.yml`, tagged `vYY.M.D.B` with the `legacyforgepatcher.jar` for `-javaagent`, which holds the library of every platform, and one `legacyforgepatcher-<RID>.so` per platform for `-agentpath`. The build workflow combines the jars that each platform's publish writes with JvmBridge's `JvmBridgeMergeAgentJars` target, once, so the tests and the release use the same file. When main has changed a workflow since the commit a run tests, GitHub won't let the run's token create the release's tag, so that run skips publishing and the newer commit's run publishes. There are no release PRs or release-please workflows. Versions follow NetAgents' UTC `YY.M.D.B` scheme, with the timestamp frozen once per workflow run. Never rebuild agents in the publishing job, publish unvalidated artifacts, print tokens, or copy organization secrets into this repository.

## Documentation

Keep README commands executable and aligned with the release assets. Bold documentation link captions and do not put inline code inside link captions. Claim only the Forge versions, Java versions and platforms that tests or recorded checks actually covered.
