// HizenLabs.Bundler — Roslyn source bundler.
//
// Phase 2 (next): given a plugin project + the shared library, produce a single
// self-contained .cs per target (Carbon / Oxide) by:
//   1. loading all syntax trees (incl. source-generator output) via the Roslyn workspace,
//   2. computing reachability from the plugin's public class through the shared types,
//   3. inlining only the reachable shared types as nested `private` members,
//   4. rewriting the namespace and merging/deduping `using` directives,
//   5. emitting dist/<Plugin>.cs that compiles standalone.

Console.WriteLine("HizenLabs.Bundler — scaffolded; merge logic not yet implemented.");
return 0;
