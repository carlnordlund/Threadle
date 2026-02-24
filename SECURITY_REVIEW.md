# Security Review: Threadle

**Date:** 2026-02-24
**Reviewer:** Claude (AI security review)
**Branch:** `claude/threadle-security-review-8mqjQ`
**Scope:** Full codebase — `Threadle.Core` and `Threadle.CLIconsole`

---

## Executive Summary

Threadle is a local-only, single-user CLI tool for network analysis. It has no network communication, no database, no authentication layer, and zero external NuGet dependencies. The overall security posture is **good** for its intended use case.

Two **data-integrity bugs** were found in the binary serializer that can cause silent data corruption or unreadable files when certain edge-case inputs are used. One **denial-of-service** path exists through recursive script loading. All other findings are either low-severity or by-design for a local tool.

---

## Findings

### [HIGH] Binary serializer: `WriteString` null/length-prefix mismatch

**File:** `Threadle.Core/Utilities/FileSerializerBin.cs`
**Lines:** 591–617

`WriteString` handles a null value by writing the integer literal `-1` with `writer.Write(-1)`, which emits **4 bytes** (as `int32`). `ReadString` always reads only **1 byte** for the length prefix (`reader.ReadByte()`). If a null string is ever written, the reader will consume the wrong number of bytes and all subsequent fields in the file will be mis-aligned, producing a corrupt or unreadable file.

In current code, the only callers of `WriteString` with potentially-null input are:
- `WriteNetworkToFile` → `network.Nodeset.Filepath` — guarded at the call-site in `SaveNetwork` (the save is aborted if `Filepath` is null or empty), so this path is currently safe.

The latent bug remains, however. Future refactors that add new callers, or that relax the existing guard, will silently corrupt binary files.

**Recommendation:** Remove the special-case null branch entirely (null names should not reach serialization), or align the null representation with the 1-byte length convention already used for reading (e.g., write `(byte)0` for null/empty).

---

### [HIGH] Binary serializer: string length silently truncated to 255 bytes

**File:** `Threadle.Core/Utilities/FileSerializerBin.cs`
**Line:** 600

```csharp
writer.Write((byte)bytes.Length);   // silent truncation if UTF-8 length > 255
```

Casting `bytes.Length` (an `int`) to `byte` wraps silently: a UTF-8 encoding of 256 bytes produces a length byte of `0`, which will be read back as an empty string. Encodings of 257+ bytes will produce the wrong length and corrupt the rest of the stream.

Network names, nodeset names, layer names, and file paths are all written through `WriteString`. A user who provides a name longer than 255 UTF-8 bytes will see silent data loss or a corrupt file on reload.

**Recommendation:** Add a guard before writing:

```csharp
if (bytes.Length > 255)
    throw new InvalidOperationException(
        $"String '{value}' exceeds the 255-byte limit for binary serialization.");
writer.Write((byte)bytes.Length);
```

Or widen the length prefix to `ushort` (2 bytes) consistently in both `WriteString` and `ReadString`.

---

### [HIGH] Binary serializer: hyperedge names use incompatible write path

**File:** `Threadle.Core/Utilities/FileSerializerBin.cs`
**Line:** 571

```csharp
// Write the name of the hyperedge
writer.Write(hyperName);            // ← BinaryWriter.Write(string): 7-bit variable-length prefix + UTF-8
```

All other strings in the same file are written with the private helper `WriteString` (1-byte fixed length + ASCII bytes), and all strings are read back via `ReadString` (1-byte fixed length + ASCII bytes). Hyperedge names are the sole exception: they are written with `BinaryWriter.Write(string)`, which uses a **7-bit variable-length integer** length prefix and **UTF-8** encoding.

For short, ASCII-only hyperedge names (< 128 bytes), the first byte of the 7-bit encoding happens to equal the string length, so `ReadString` will accidentally read the correct number of bytes. For names ≥ 128 bytes, or any name containing non-ASCII Unicode characters, the binary stream will be misread and all subsequent data in that layer will be corrupted.

**Recommendation:** Replace `writer.Write(hyperName)` with `WriteString(writer, hyperedgeName)` to use the same encoding path as all other strings.

---

### [MEDIUM] `loadscript` has no recursion depth limit

**File:** `Threadle.CLIconsole/Runtime/ScriptExecutor.cs`
**File:** `Threadle.CLIconsole/Commands/LoadScript.cs`

A script file can contain a `loadscript()` call pointing to itself or to another script that eventually calls back into the first. There is no cycle detection and no maximum nesting depth. This will exhaust the call stack and throw a `StackOverflowException`, which the .NET runtime does not allow to be caught; the process terminates abruptly.

In a purely local, single-user context the impact is limited to a self-inflicted crash. However, if Threadle is ever invoked non-interactively from a parent process (e.g., an R wrapper, a CI pipeline, a web back-end), a maliciously or accidentally constructed script file could terminate the host process.

**Recommendation:** Track script load depth in `CommandContext` and return a `CommandResult.Fail` error if the depth exceeds a reasonable limit (e.g., 32).

---

### [LOW] `dir()` command lists any readable filesystem path

**File:** `Threadle.CLIconsole/Commands/Dir.cs`
**File:** `Threadle.Core/Utilities/FileManager.cs` — `GetDirectoryListing`

The `dir` command passes the user-supplied `path` argument directly to `Directory.GetDirectories` / `Directory.GetFiles` with no restriction. A user can call `dir(path="/etc")` or `dir(path="C:\\Windows\\System32")` and receive a listing of any directory that the OS process has read access to.

For a single-user local tool this is accepted behaviour (the user already has OS-level access). The concern arises if Threadle is embedded in a multi-tenant or server-side context (e.g., the R extension exposed via a web API), where information disclosure about the server filesystem would be undesirable.

**Recommendation:** For embedded/server deployments, pass a `baseDirectory` constraint into `GetDirectoryListing`, analogous to the existing guard in `SafeSetCurrentDirectory`. No action required for local interactive use.

---

### [LOW] `setwd` documents `~examples` but does not implement it

**File:** `Threadle.CLIconsole/Commands/SetWorkingDirectory.cs`
**Lines:** 16–40

The `Syntax` property documents three special values: `~`, `~documents`, and `~examples`. The implementation handles `~` and `~documents` but falls through for `~examples`, passing the literal string `~examples` to `SafeSetCurrentDirectory`, which will fail with `FileNotFound` unless a directory literally named `~examples` exists.

This is a correctness / documentation mismatch rather than a security issue, but it could confuse users or downstream tooling.

**Recommendation:** Either implement the `~examples` path resolution (pointing to the `Threadle.CLIconsole/Examples/` directory) or remove it from the syntax documentation.

---

### [LOW] Exception messages exposed to the user may contain filesystem paths

**Files:** `Threadle.CLIconsole/Results/JsonCommandResultRenderer.cs:55`, `TextCommandResultRenderer.cs:52`

Unhandled exceptions are caught in the `CommandLoop` and rendered to the user via `ex.Message`. .NET I/O exceptions frequently include absolute file paths in their messages (e.g., `"Could not find file '/home/alice/sensitive_path/file.bin'"`). In the interactive local case this is useful for debugging. In a server-side or JSON-API deployment, this leaks host filesystem layout to the caller.

**Recommendation:** For JSON API deployments, strip or sanitize exception messages before output, or return a generic "internal error" message with a logged correlation ID.

---

## What Was Reviewed and Found Secure

| Area | Finding |
|------|---------|
| External dependencies | Zero NuGet packages — no supply-chain attack surface |
| Network communication | None — completely local, no HTTP/socket code |
| Authentication / authorization | Not applicable (local single-user tool) |
| SQL / command injection | Not applicable — no SQL, no shell invocation |
| Input parsing (CLI text) | Pre-compiled regex patterns; safe `TryParse` for all typed values |
| Input parsing (JSON mode) | `System.Text.Json` with case-insensitive options; null/whitespace guarded |
| File I/O resource management | All streams use `using` — no resource leaks |
| Path traversal in `setwd` | `SafeSetCurrentDirectory` canonicalises and checks against `baseDirectory` |
| Binary deserialization DoS | Magic-byte and version checks prevent arbitrary memory allocation from junk files |
| Secrets / credentials in source | None found in any source, config, or project file |
| CI/CD pipeline (`.github/workflows`) | Standard build-and-test only; no secret injection paths observed |

---

## Risk Summary

| ID | Severity | Title |
|----|----------|-------|
| 1 | **HIGH** | `WriteString` null value writes `int32` but `ReadString` reads `byte` |
| 2 | **HIGH** | `WriteString` silently truncates strings longer than 255 bytes |
| 3 | **HIGH** | Hyperedge names written with incompatible `BinaryWriter.Write(string)` API |
| 4 | **MEDIUM** | `loadscript` recursion can crash the process with `StackOverflowException` |
| 5 | LOW | `dir` command lists any accessible filesystem path |
| 6 | LOW | `~examples` documented in `setwd` but not implemented |
| 7 | LOW | Exception messages may expose filesystem paths in server deployments |
