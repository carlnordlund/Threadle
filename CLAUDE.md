# Notes for Claude

## Future ideas (not yet implemented)

- **Per-call decimal-separator option for file import/export.** Threadle's float/double parsing
  and formatting was fixed to always use `CultureInfo.InvariantCulture` (always period `.` as the
  decimal separator, regardless of the running machine's locale) - see the `claude/nodeattr-culture-fix`
  branch (`Threadle.Core/Utilities/Misc.cs`, `LayerImportExport.cs`,
  `Threadle.CLIconsole/Parsing/CommandPackage.cs`). That's the right default and should stay the
  default: it keeps exported files portable and avoids ambiguity with the CLI's comma-as-argument-separator
  syntax.
  However, file **import** of externally-sourced data (e.g. a matrix/edgelist file from a
  collaborator using Excel in a European locale, typically semicolon-separated with comma
  decimals) currently can't be read if it uses comma decimals. Worth adding, later, as a scoped,
  explicit **per-call** option on the relevant import (and maybe export) commands - e.g.
  `importlayermatrix(file, separator=';', decimalseparator=',')` - rather than a global
  `setting()`. A global toggle would reintroduce the same "silently different behavior depending
  on ambient state" problem the culture fix just removed; a per-call parameter keeps the choice
  explicit and scoped to that one file.
