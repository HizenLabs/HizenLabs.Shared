# Contributing to HizenLabs.Shared

Contributions are welcome: bug fixes, new shared utilities, generator/bundler
improvements.

## One thing to know first: the CLA

`HizenLabs.Shared` is owned by **Aerial Byte LLC** (operating the HizenLabs brand)
and is **dual-licensed** (GPLv3 publicly, plus commercial terms so it can be used in
the proprietary HizenLabs Premium plugins). To keep that possible, every contributor
must agree to our [Contributor License Agreement](https://gist.github.com/EthanDelong/7e7e19ea155c4dfc6d0317747629c1ed)
before a change can be merged. It lets you keep your copyright while granting Aerial
Byte LLC the right to relicense your contribution.

Opening a pull request confirms you agree to the CLA.

## Workflow

1. Open an issue first for anything non-trivial so we can align on the approach.
2. Build locally: `dotnet build HizenLabs.Shared.slnx` (see the [README](README.md)
   for fetching the Rust reference assemblies).
3. Keep changes focused; match the surrounding style.
4. Open a PR describing what changed and why.
