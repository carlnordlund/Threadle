# Changes

## 1.3.1

### New features

- **`setting()` gained `executiontime`** (boolean, default `false`): when enabled,
  each dispatched command's wall-clock execution time (in milliseconds) and name
  are measured and stored internally. **New `lastexecutiontime()` command**
  retrieves the most recently measured value — both as a human-readable message
  (`Last execution time: 12.34 ms (betweennesscentrality).`) and as a
  structured payload (`ExecutionTimeMs`, `Command`), machine-readable like any
  other command's metrics. If the setting is off, or nothing has been measured
  yet, it says so instead, with no payload — turning the setting off clears
  any previously measured value immediately, so `lastexecutiontime()` never
  keeps answering from before the setting was disabled. No threadleR wrapper
  is needed — call it via the existing `th_cmd("lastexecutiontime")` escape
  hatch.
- **New `memoryinfo()` command**: reports `ManagedMemoryBytes` (live managed
  heap, `GC.GetTotalMemory`), `GCHeapSizeBytes` (total committed managed
  heap, `GC.GetGCMemoryInfo().HeapSizeBytes`), and `WorkingSetBytes` (the
  process's OS-reported resident memory) — useful when benchmarking memory
  externally (e.g. inspecting the Threadle process from Python), since
  resident memory can sit well above what's actually live until the .NET
  GC returns freed pages to the OS, which it doesn't do reliably on an
  ordinary collection. Passive by default (same as `systeminfo()`); an
  optional `forcegc=true` first runs a blocking, compacting Gen2 collection
  — the specific combination that reliably decommits freed segments — for
  an accurate snapshot. Not cheap: a full compacting collection can pause
  the process noticeably on a large heap, so `forcegc=true` is meant to be
  used deliberately, not in a tight loop. No threadleR wrapper needed —
  call via `th_cmd("memoryinfo")`.

### New capability on existing commands

> No new commands. Every change below loosens an existing restriction — existing
> 1-mode calls to any of these commands are unaffected.

- **`diameter()`/`eccentricity()`** now accept a 2-mode layer directly, pseudo-projected
  (traversed node-hyperedge-node, without materializing a 1-mode projection).
  `components()` and `shortestpathsattributes()` turn out to already have had no gate
  rejecting a 2-mode layer either — though their own traversal needed a Performance fix
  (below) to actually get the same pseudo-projected cost as these two.
- **`assortativity()`/`degreeassortativity()`** now accept a 2-mode layer, with a new
  `projection = ['binary'(default),'count']` parameter controlling how a pair of
  co-affiliates (members sharing a hyperedge) is counted: `binary` counts a pair once
  regardless of how many hyperedges it shares; `count` counts it once per shared
  hyperedge (matching `getedge()`'s count-weighted projection). Degree itself is always
  the existing binary projected degree (distinct co-affiliate count) regardless of
  `projection`.
- **`communitydetection()`** (`type = 'louvain'/'leiden'/'lp'/'lpam'`) now accepts a
  single 2-mode layer on its own (not combined with other layers), genuinely
  pseudo-projected — no pairwise structure, not even an ephemeral in-memory one, is
  ever materialized. Tie strength is always count-weighted (matching
  `eigenvectorcentrality()`'s convention) — no `projection=` parameter here: local
  moving itself already costs O(sum of hyperedge sizes squared) *per sweep*, not
  O(sum of hyperedge sizes) — it calls the same non-memoized per-node lookup
  `degree()`/`pagerank()` use, repeated every sweep until convergence — so a 2-mode
  layer's level-0 pass does not get the same clean linear-time advantage
  `diameter()`/`components()` get, though it still avoids materializing anything, so
  the memory advantage holds. A binary option would cost about the same at local
  moving; it would add a further, smaller, one-time O(sum of hyperedge sizes squared)
  pass at aggregation specifically, to recover cross-hyperedge pair identity the
  per-hyperedge community histogram otherwise discards — not the reason binary isn't
  offered on its own, since local moving's own recurring cost is paid either way.
  `type = 'gn'` (Girvan-Newman) has no 2-mode
  support: edge betweenness has no hyperedge analogue. For `type = 'leiden'` on a
  2-mode layer, only the very first (hyperedge-direct) level skips the usual
  connectivity-guaranteeing refinement step, so that guarantee is **not** established
  for a community formed entirely within that first pass — refinement runs normally
  from the first aggregation onward.
- **`constraint()`/`effectivesize()`** now accept a single 2-mode layer on its own, with
  a new `samplesize` parameter that becomes **required** (`samplesize > 0`) for a
  2-mode layer: only that many randomly sampled nodes get an exact (not approximated)
  value, since a 2-mode ego network's cost grows with the square of its (potentially
  large) projected degree. `samplesize` is ignored for 1-mode layers, which are still
  always computed exactly for every node.
- **`dyadcensus()`** now accepts a 2-mode layer: since an affiliation has no direction,
  the result is the same trivially-all-mutual shape as an undirected 1-mode layer.
- **`getlayerstats()`** now accepts a 2-mode layer: instead of edge-value statistics, it
  reports Mean/Median/StdDev/Min/Max/Q1/Q3 over hyperedge *sizes*, plus `NbrHyperedges`,
  `NbrAffiliations`, and two estimates meant to help decide whether `projecttwomode()`
  is memory-feasible before calling it: `EstimatedProjectedEdgesUpperBound` (a safe
  upper bound on the number of distinct projected edges, dominated by the single
  largest hyperedge) and `EstimatedProjectionMemoryBytes` (a rough, order-of-magnitude
  byte estimate from that bound) — neither is a precise prediction, just a smell test.
- **`density()`** on a 2-mode layer can now be computed exactly on request
  (`samplesize=0`), rather than only ever being a sampled estimate. The default stays a
  200-node sample, though: unlike most other `samplesize`-bearing commands, exact 2-mode
  density is deliberately *not* the default here, since it needs every node's true
  deduplicated degree — O(sum of hyperedge sizes squared), the same order as
  materializing a full projection, not the O(sum of hyperedge sizes) a "nothing gets
  materialized" framing might otherwise suggest.
- **`diameter()`** gains a new `diameteronly` parameter (default `false`). The default
  mode is completely unchanged — still computes `Diameter` and `AvgDistance`/
  `PairsConsidered` together, at the same cost as before. `diameteronly=true` trades
  `AvgDistance`/`PairsConsidered` away for a faster diameter-only computation: an exact
  mean over every pair inherently needs a full BFS from every node regardless (no
  shortcut exists for a sum the way one exists for a max), so this is a genuine opt-in,
  not a transparent speed-up of the default. When the layer/traversal combination is
  effectively symmetric (a 2-mode layer, a symmetric 1-mode layer, or an explicit
  `traversal=both`), this uses the iFUB algorithm (Crescenzi, Grossi, Lanzi & Marino
  2012) — exact, but often examining only a small fraction of a component's nodes on
  real-world graphs (no improved worst-case bound — e.g. a path graph still needs most
  nodes examined — so this is an empirical/typical-case win, not an asymptotic one).
  Otherwise (a directed layer queried with `out`/`in` traversal, where distances aren't
  symmetric and iFUB's exactness proof doesn't apply) falls back to the same exhaustive
  all-pairs pass as the default mode, same cost, just without `AvgDistance`/
  `PairsConsidered` in the result. Either way the result also carries `Algorithm`
  (`"iFUB"` or `"AllPairs"`) so a caller can see which path actually ran.

### Behavior changes

- **The `onlyoutboundedges` setting was removed.** It let a directed 1-mode layer
  skip its per-node inbound edge index while dynamic, to save memory for
  walker-only workloads — but it was a global, live-toggled flag with no
  safeguard against leaving a single layer with inconsistent per-edge inbound
  coverage (edges added before a mid-session toggle kept their inbound entry,
  edges added after didn't), and packing the same layer to its static (CSR)
  form turns out to be the more memory-efficient choice for that workload
  anyway — walker operations (`getrandomalter()`/`rwfpt()`) already work
  correctly and efficiently on packed layers, which always maintain both
  directions regardless of this setting. Directed layers now always maintain
  both directions while dynamic too, matching what packed layers already did
  unconditionally.

### Performance

- **`projecttwomode()`** no longer builds a single global dictionary holding every
  distinct projected pair before writing the result — that dictionary coexisting with
  the layer being built from it was the actual peak-memory driver. It now computes each
  node's projected edges directly, one node at a time, writing them straight into the
  result; total work stays the same, but peak memory is now roughly the finished
  layer's own size plus one node's hyperedges at a time, instead of also holding every
  projected pair in memory at once. No behavior change — every method (`count`,
  `binary`, `newman`) produces identical results to before.
- **`projecttwomode()`** gains a new `lowmemory` parameter (default `false`). When
  `true`, it instead takes two passes to build the result directly as a packed
  (static) layer, with no dynamic layer ever created along the way: the first pass
  sizes each node's final slot, the second fills it, so peak memory is roughly just
  the finished layer's own size (no list-growth slack, and nothing built-then-packed-
  afterward that would briefly double up). Costs roughly twice the time of the default
  path, since each node's projected edges are computed twice. Same result either way —
  check `getlayerstats()`'s `EstimatedProjectedEdgesUpperBound`/
  `EstimatedProjectionMemoryBytes` on the 2-mode layer first to decide whether this is
  worth turning on.
- **`closenesscentrality()`/`harmoniccentrality()`** on a 2-mode layer no longer pay
  for betweenness-style bookkeeping they never use. Both only ever need shortest-path
  *distances*, not path *counts*, but previously shared `betweennesscentrality()`'s
  BFS pass unconditionally — which, on a 2-mode layer, must re-scan a hyperedge's
  member list once per same-level arriving member to keep Brandes' path count
  correct (worst case O(sum of hyperedge sizes squared)). Both now use the plain
  (count-free) BFS traversal instead — the same one `diameter()`/`eccentricity()`
  already use — genuinely linear in hyperedge sizes. `betweennesscentrality()` is
  unaffected; no behavior change for either — same distances either way, just a
  cheaper traversal.
- **`assortativity()`/`degreeassortativity()`** with `projection='count'` on a
  2-mode layer no longer enumerate co-affiliate pairs one by one. A hyperedge's
  Count-weighted contribution to every needed Pearson moment can be folded from two
  per-hyperedge running sums (Σx and Σx² over its members) in one pass, since
  Count-weight contributions from different hyperedges are simply additive — unlike
  `binary`, which genuinely needs to know individual pair identity across
  hyperedges and keeps the previous per-node enumeration. Lowers `count`'s cost from
  O(sum of hyperedge sizes squared) to O(sum of hyperedge sizes). No behavior
  change — identical correlation value either way.
- **`components()`/`shortestpathsattributes()`** on a 2-mode layer no longer
  re-derive each node's alters node-by-node (which, via the non-memoized
  `GetNodeAlters`, costs O(sum of hyperedge sizes squared) in aggregate across a
  traversal). Both now use the same hyperedge-aware BFS primitive `diameter()`/
  `eccentricity()` already use, which gates each hyperedge behind a per-traversal
  visited set so it is expanded at most once — genuinely O(sum of hyperedge sizes).
  No behavior change — identical component assignments and shortest-path distances
  either way, just a cheaper traversal.
- **`pagerank()`** is now parallelized (up to `maxthreads`, see `setting()`), same as
  `betweennesscentrality()`/`closenesscentrality()`/`harmoniccentrality()` already
  were — it was the one centrality measure (besides `eigenvectorcentrality()`) with no
  parallel construct anywhere. Both the one-time out-neighbor precompute (each node
  writes only its own disjoint output slot, no merge needed) and the per-iteration
  update (a scatter-add, using the same thread-local-accumulator-merged-under-lock
  pattern the other centralities already use) are parallelized; the outer iteration
  loop itself stays sequential, since each iteration needs the previous one's fully-
  merged result. No behavior change in the converged value — floating-point summation
  order can differ slightly between a sequential and parallel run, same as any other
  parallel reduction in this codebase, but this doesn't change the result beyond
  ordinary floating-point rounding.

### Internal cleanup

- **`coreness()`, `clusteringcoefficient()`, `transitivity()`, and community
  detection's Girvan-Newman path** now share one helper
  (`ResolveOneModeOnlyLayers`) for "resolve these layer names (or all layers),
  rejecting any 2-mode layer" instead of four separately hand-written copies of
  the same loop. No behavior change to any of these commands' results; the one
  user-visible effect is that `transitivity()`'s and Girvan-Newman's 2-mode
  rejection error messages are now as specific as `coreness()`'s/
  `clusteringcoefficient()`'s already were (both previously gave a terser
  message that didn't mention `projecttwomode()`) — confirmed no test asserted
  on the old wording. Also removed ~220 lines of dead, commented-out code in
  `Analyses.cs` (an old `ShortestPath` implementation superseded by
  `PathFunctions.BidirectionalBFS`, never deleted after that refactor).
- **`constraint()`/`effectivesize()` and community detection's Louvain/Leiden/
  LabelPropagation/LPAm** similarly now share one helper
  (`ResolveSingleTwoModeOrManyOneModeLayers`) for their "a single 2-mode layer
  on its own, or any number of 1-mode layers, never mixed" layer-resolution
  policy, instead of two separately hand-written near-duplicates. No behavior
  change; the one user-visible effect is that `constraint()`'s/
  `effectivesize()`'s "a 1-mode layer was specified after a 2-mode layer"
  error now mentions the `samplesize > 0` requirement, matching the wording
  their other 2-mode-conflict error already used (confirmed no test asserted
  on the old wording).
- **`shortestpathsattributes()`/`rwfpt()`** now dispatch through
  `Analyses.cs` (new thin wrappers `ShortestPathsAttributes`/
  `RandomWalkerFirstPassageTime`), like every other CLI command, instead of
  calling into `Distance.cs` directly — restores the one-facade rule for
  the CLI layer. `Distance.cs` itself is unchanged and remains the real
  implementation file for both.

### Fixes

- **`constraint()`'s CLI command name was wrong.** The dispatcher had
  registered it under the key `"constraints"` (plural) — to dodge a
  same-name collision between the `Commands.Constraint` class and
  `System.Data.Constraint` — but the command's own `Syntax`/help text
  always advertised the singular `constraint(...)`, which didn't actually
  work. The collision was only ever about the C# *type* name, not the
  dispatch *string*, so the key is now simply `"constraint"`, matching the
  class, the `Syntax` string, and every other command's convention. (No
  real users depend on `constraints(...)` yet, since this hasn't shipped.)

### Deliberately out of scope

- **`clusteringcoefficient()`/`transitivity()`** stay 1-mode only: projecting a 2-mode
  layer and running the existing clique-counting formula on it would be actively
  *wrong* (every hyperedge becomes a clique, trivially inflating clustering), not
  merely unsupported — a genuinely different two-mode-native measure would be needed
  instead.
- **`coreness()`** stays 1-mode only for now — k-core peeling's interaction with
  pseudo-projected degree on node removal needs its own careful design.
- **`degree()`** stays binary-only (distinct-alter count) on a 2-mode layer for now.
  A `count` (node-strength) variant — summing shared-hyperedge counts instead of
  deduplicating alters — would be a straightforward O(sum of hyperedge sizes)
  addition: visit each hyperedge once and add `k−1` to every member's running
  total, no pair enumeration needed. Identified and verified sound, but deliberately
  deferred past this freeze, since it would be a new quantity (weighted node
  strength), not a faster version of the existing distinct-alter-count degree.
- **`pagerank()`** stays binary-only (deduplicated transition structure) on a
  2-mode layer for now. A `count` (shared-hyperedge-weighted) variant could use the
  same per-hyperedge closed form `eigenvectorcentrality()` already uses — within
  one hyperedge, each member's incoming-mass contribution is one scalar sum over
  that hyperedge's members, minus its own term, genuinely O(sum of hyperedge sizes)
  per iteration. Identified and verified sound, but deliberately deferred past this
  freeze, since it would be a genuinely different weighted-PageRank variant, not
  the same quantity computed faster.
- **`smallworldcoefficient()`** stays 1-mode only: it depends on both
  `clusteringcoefficient()` (above) and `rewire()`'s null model, which has no defined
  2-mode (degree-preserving affiliation-swap) analogue.

## 1.3

### New features

- **Centrality measures**: betweenness, closeness, harmonic, eigenvector, and edge
  betweenness centrality, all multithreaded.
- **Clustering coefficient**: multiple methods (Watts-Strogatz, Fagiolo, Barrat,
  Onnela) plus overall transitivity, auto-selected by layer type unless overridden.
- **Coreness** (k-core decomposition, Batagelj & Zaversnik 2003).
- **Assortativity**: degree assortativity and attribute-based assortativity.
- **Burt's structural holes**: `constraint()` and `effectivesize()`.
- **Dyad census** (`dyadcensus()`) and **triadic census** (`triadiccensus()`), the
  latter with an optional sampled mode returning confidence intervals (Wilson score).
- **Community detection**: Louvain, Label Propagation, Leiden, and Girvan-Newman, all
  under one `communitydetection()` command (`type=` argument), multithreaded with
  optional multi-restart search.
- **Diameter** (`diameter()`): exact or sampled (two-sweep) estimation, with average
  shortest path length.
- **Eccentricity** and **small-world coefficient**.
- **Network/layer rewiring** (`rewire()`): degree-preserving 2-edge swap, or the
  3-edge (networkx-style) swap for directed layers.
- **Shortest paths**: `shortestpaths()` now computes a batch of specific node pairs
  (optionally returning the actual path); `shortestpathsattributes()` (previously
  `shortestpaths()`) computes shortest-path distance distributions between node
  attribute values, multithreaded.
- **Random walker first-passage time** (`rwfpt()`), multithreaded, with optional
  per-layer weighting (`layerweights`) and histogram output.
- **File formats**: GraphML and Pajek (.net) import/export, alongside the existing
  formats; `export()`/`importlayer()` support string node labels in place of raw ids,
  and importing an edgelist can filter rows by column value.
- **Layer operations**: `mergelayers()` (1-mode and 2-mode), `binarytovalued()`,
  `symmetrictodirected()`, `duplicate()` and `rename()` (for layers and node
  attributes).
- **Network/nodeset operations**: `mergenetworks()` and `mergenodesets()`, with
  configurable conflict handling for attributes defined in both sources.
- **Node attribute tools**: `setattrs()` (set a value across many nodes at once) and
  `getattrstable()` (tabular export of attribute values).
- **`getalterdetails()`**: detailed, row-per-relationship table of multiple nodes'
  alters across layers — suited to feeding SEIR/SIR-style epidemic models.
- **`getlayerstats()`**: summary statistics for a valued 1-mode layer's edge values.
- **`convertattr()`**: converts a node attribute to a new type, storing the result
  under a new name and leaving the original untouched. Each node's value is converted
  independently; a value that doesn't convert (e.g. a non-numeric string to `int`) is
  simply left unset on the new attribute for that node, rather than failing the whole
  command.
- **Multithreading**: configurable via `setting(maxthreads=...)`, with system core
  info via `systeminfo()`, and persistent defaults via an optional `appconfig.json`.
  Applied throughout: all centralities, clustering coefficient, constraint/
  effectivesize, triadic census, community detection, and shortestpathsattributes.

### Behavior changes

> These change the numeric result of an existing command — not new bugs. The old
> result was computed incorrectly in each case.

- **Betweenness and edge betweenness centrality on undirected layers** were
  previously normalized incorrectly (effectively double-counted), producing values
  up to 2x too large. Both now normalize correctly; as a consequence, the `directed`
  parameter on `betweennesscentrality()` no longer affects the result and has been
  removed.
- **Degree and attribute-based assortativity on undirected layers** previously only
  sampled each edge from one endpoint, which doesn't just halve the sample but changes
  the correlation itself — a pure star graph, for example, came out as 0 instead of
  the correct -1. Now correctly samples each undirected edge from both endpoints.
- **Self-loops on symmetric (undirected) layers allowing self-ties** were silently
  dropped or miscounted by several functions that enumerate or query an edge list —
  including `getalledges()`, the `preview()` edge list, `removeedge()`'s success
  reporting, and the random-network generators. All now correctly account for
  self-loops, which are stored once (not twice) per the layer's internal
  representation.
- **`constraint()`/`effectivesize()`** (Burt's structural holes) now refuse to run on
  any layer that allows self-ties, since the underlying formula has no defined
  treatment for a self-tie — even if the layer happens to have none.
- **`clusteringcoefficient()` with the Onnela method** likewise now refuses layers
  that allow self-ties, for the same reason (use `barrat` for such layers instead).
- **`rwed` (random walker exposure distance)** was removed; only `rwfpt` (first-
  passage time) remains.

### Fixes

- Fixed hyperedge membership corruption in 2-mode layers when an import source (or a
  raw `.bin` file) contained a duplicate node id within one hyperedge's row — this
  could overcount shared affiliations between two nodes and, in one code path, cause
  an infinite loop when picking a random alter.
- Fixed `symmetrize()` with the `sum` method producing a binary layer instead of the
  intended valued one.
- Fixed a default-settings bug in eigenvector centrality.
- Fixed `GenerateErdosRenyiLayer`/`GenerateBarabasiAlbertLayer`/
  `GenerateWattsStrogatzLayer`: self-ties could be double-added on symmetric layers,
  `GenerateWattsStrogatzLayer` had no validation that `k` fit the network size, and
  `GenerateBarabasiAlbertLayer` cleared a layer's existing edges *before* validating
  its `m` parameter, destroying data even when the call failed.
- Fixed `rewire()`: picking two existing self-loops as the pair to swap could merge
  them into a single ordinary edge, silently dropping the layer's edge count by one
  and (for a valued layer) discarding one of the two original weights.
- Fixed loading a `.tsv` nodeset or network file crashing with an out-of-range error
  when a data row had more columns than the header defined.
- Fixed file save/load failing with "unsupported format" for any filename containing
  more than one dot before its real extension (e.g. `my.network.tsv`).
- Fixed import/filter value parsing stripping a leading or trailing quote character
  even when it wasn't paired with a matching closing quote — corrupting a value that
  legitimately started or ended with an apostrophe or curly quote (e.g. `'80s Club`).

### Known limitations

- GraphML has no native character attribute type, so round-tripping a `Char` node
  attribute through `export(...,format=graphml)`/`importnetwork()` converts it to a
  `String` (the value survives; the type does not). This is a limitation of the
  GraphML format, not a bug — use `convertattr(...,attrtype=char)` to restore it to a
  `Char` attribute (under a new name) after import.
- Exact reproducibility via `randomseed()` for community-detection multi-restart
  search and diameter's sampled mode is only guaranteed when `maxthreads` is set to 1,
  since the underlying random number generator is not seeded per worker thread.
