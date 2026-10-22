# Repository guidelines

LegacyForgePatcher is a NativeAOT JVM agent, written in C# with JvmBridge, that fixes launch failures of legacy Forge on current Java. Each fix targets the exact upstream code it repairs and leaves every other class unchanged.

## Layout

- `src/LegacyForgePatcher`: the agent and its class file reading and writing.
- `tests/LegacyForgePatcher.Tests`: xUnit unit tests of class file patching, referencing the agent project.
- `tests/LegacyForgePatcher.IntegrationTests`: xUnit tests that load the published agent into real JVMs, against Java fixtures and real Forge servers.
- `.github`: reusable CI, GitHub releases, dependency maintenance, and repository templates.

## Source conventions

Use four-space C# indentation, descriptive identifiers, underscore-prefixed private fields, and camelCase parameters. Put braces on their own lines and omit braces for single-line statement bodies. Keep method signatures on a single line. Prefer pattern matching and nameof. Async methods use an Async suffix. Never silence nullable warnings with the null-forgiving operator. NetAgents formats sources on build; review what it rewrites.

Don't add `global.json`, NuGet lock files, or other .NET JSON configuration. CI pins the SDK in its workflows.

## Native correctness

- Keep every agent callback non-throwing in effect: patch only classes whose bytes match the expected upstream shape, and leave anything else unchanged.
- Generated classes must load on Java 6 and later; prefer straight-line code, which needs no stack map frames.
- Class files store modified UTF-8 without JNI's zero terminator.
- Never call ordinary JNI from Agent_OnLoad. Define classes from `OnVmInit` or later.
- Keep the agent free of host requirements such as libicu: JVM hosts are often slim images.

## Verification

Install the .NET 10 SDK, Clang and zlib development headers. Run `dotnet build LegacyForgePatcher.slnx -c Release` and `dotnet test LegacyForgePatcher.slnx -c Release --no-build`, which skips the native tests. Publish the agent with `dotnet publish src/LegacyForgePatcher -c Release -r <RID> -o artifacts/agent/<RID>`, then run `dotnet test tests/LegacyForgePatcher.IntegrationTests -c Release` with `TARGET_RID=<RID>` and `JAVA_HOME` set to a Java 8 JDK. The Forge server test needs Docker. Record which platforms and Java versions actually executed.

CI must account for every configured platform. Never weaken tests or skip source, build or workflow changes to get green checks.

## Commits and releases

Use Conventional Commits with a scope, a gitmoji after the scope, and past-tense subjects for completed work. Allowed types: feat, fix, perf, deps, revert, docs, style, chore, refactor, test, build, ci. Use feat/fix for production source behavior. Include a commit body describing observable behavior changes, including "none" when appropriate.

Examples: `fix(agent): 🐛 patched FML's tweak sort on Java 8u20 and later`, `ci(test): ✅ started Forge servers with the published agent`.

Pull request descriptions use the repository template. Every validated main commit publishes the agents it tested as a GitHub release through `publish-github.yml`, tagged `vYY.M.D.B` with assets named `legacyforgepatcher-<RID>.so`. There are no release PRs or release-please workflows. Versions follow NetAgents' UTC `YY.M.D.B` scheme, with the timestamp frozen once per workflow run. Never rebuild agents in the publishing job, publish unvalidated artifacts, print tokens, or copy organization secrets into this repository.

## Documentation

Keep README commands executable and aligned with the release assets. Bold documentation link captions and do not put inline code inside link captions. Claim only the Forge versions, Java versions and platforms that tests or recorded checks actually covered.
