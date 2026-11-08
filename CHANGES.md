# Changes

## 1.3.1

### New features

- **`communitydetection()` gained `maxsweeps`** (default 0 = no limit), for
  `louvain`, `plm`, `leiden` and `lpam`, on 1-mode and 2-mode layers: caps the
  number of local-moving sweeps per level. By default local moving repeats
  until a full sweep moves no node; on weakly structured networks most of the
  time can go into long tails of sweeps that each move only a few nodes. With
  the default, results are unchanged (verified byte-for-byte against the
  previous build). Measured with `maxsweeps=32` on 100,000-node test networks:
  scale-free `louvain` 11.3 s → 1.7 s, `leiden` 11.6 s → 2.2 s, `lpam`
  10.7 s → 1.5 s; random `louvain`/`leiden`/`lpam` about 3.5× faster; a
  small-world network with clear communities and a random 2-mode network
  hardly changed (local moving converged within 32 sweeps). Modularity moved
  by a few percent either way for `louvain`/`leiden` (within run-to-run
  variation), but `lpam`, having a single level, lost up to 7% on the
  scale-free network. For `plm`, `maxsweeps` replaces its own default cap of 32.

- **New community detection type `plm`** (`communitydetection(..., type=plm)`):
  the parallel Louvain method of Staudt & Meyerhenke (2016), as in NetworKit.
  Same multi-level local moving and aggregation as `louvain`, but each
  local-moving sweep processes nodes in parallel across up to `maxthreads`
  threads, sharing one community assignment without locking around each move
  (per-community totals are updated atomically; sweeps per level are capped at
  32). Works on 1-mode layers and on a single 2-mode layer, with the same
  `resolution`; `numrestarts` runs one after another. Modularity is typically
  close to `louvain`'s, but results can differ between runs even with the same
  `randomseed()` unless `maxthreads` is 1. `louvain` itself is unchanged.
  Measured on 4 cores: a 300,000-node random 1-mode network (≈5M edges)
  `louvain` 92 s, `plm` 6 s, modularity 0.2429 vs 0.2428; a 382,000-node random
  2-mode network `louvain` 23.5 s, `plm` 13.0 s.

- **New `setattrstorage()` command**: `setattrstorage(structure,
  storage = ['sparse','dense','auto'], *attrname)` converts node attributes
  between sparse and dense storage in place — same names, types and values.
  - **With `attrname`**, only that attribute changes. `'auto'` picks the
    storage estimated to use less memory for it: dense costs 4 bytes plus
    1 bit for every node, sparse about 5 bytes per stored value (more where
    it is a node's only value). In practice dense wins once more than about
    80% of nodes have a value. The estimate depends only on which nodes have
    values, not on the other attributes' current storage, so converting
    attributes one at a time gives the same result in any order. The message
    includes both estimates.
  - **Without `attrname`**, all attributes change: `'sparse'`/`'dense'` set
    every attribute to that storage, and `'auto'` picks the combination
    estimated to use the least memory for the nodeset as a whole. It
    compares the exact total of "the j most populated attributes dense, the
    rest sparse" for every j, so it also accounts for costs that only exist
    while some attribute is sparse (e.g. the 8-byte-per-node array of
    references to sparse values, released once no sparse value is left).
    The message reports the estimate before and after and which attributes
    ended up dense and sparse.
  - If an attribute already has the chosen storage, it is left unchanged.

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

- **`defineattr()` gained an optional `storage` argument** (`'sparse'` (default)
  or `'dense'`). Sparse attributes keep values per node, only for nodes that
  have them set — suited to register-style attributes that only a subset of
  nodes have. Dense attributes keep values in one flat column with an entry per
  node plus a presence bitmap — suited to attributes (almost) every node has.
  Both behave identically otherwise: a dense attribute can still be unset for
  a node, so `getattr()`, `removeattr()`, `filter()` with `isnull`/`notnull`,
  `convertattr()` etc. work exactly as before. The storage mode is shown by
  `info()`. Attributes created by analyses (degree, centralities, components,
  etc.) and by `generateattr()` are now dense; an already existing attribute
  keeps its storage mode. Imported attributes (`importnodeattributes()`,
  GraphML/Pajek import) are sparse; `setattrstorage()` can convert them. `duplicate(type=attribute)` and `convertattr()` copy the storage mode
  of the source attribute, and merging nodesets yields a dense attribute if it
  is dense in either source.

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
  - **iFUB fixed to actually stop early.** As first implemented it gave exact
    results but examined nearly every node, so `diameteronly=true` was no faster
    than the exhaustive pass: its stopping test used the bound `(i−1) + ecc(root)`
    instead of iFUB's `2(i−1)`, and it started from a peripheral root (the far end
    of a double sweep), whose eccentricity is close to the diameter. It now uses
    `2(i−1)` and starts from a central root — the midpoint of the double-sweep
    path, found with one extra BFS — with the double sweep's longest distance as
    the initial lower bound, as iFUB is designed. Still exact for any root; on a
    382,000-node 2-mode test network with a sparse periphery, the exact diameter
    now takes 2.5 s where it previously ran for over 5 minutes without finishing.
    On graphs with no periphery (e.g. uniformly random ones, where almost every
    node is equally far from everything) iFUB still has to examine a large share
    of the nodes.

### Behavior changes

- **The `nodecache` setting was removed; the node-id array is always
  cached.** A nodeset's array of all node ids is built the first time it
  is needed and kept until a node is added or removed (about 4 bytes per
  node). Turning the cache off saved only that memory, but made every use
  of the array rebuild it, which many analyses do repeatedly.
  `setting(nodecache, …)` now fails as an unknown setting. The remaining
  settings are `maxthreads`, `verbose` and `executiontime`.
- **Multiedges are never allowed; the `blockmultiedges` setting was
  removed.** Adding an edge through the public API (`addedge()`,
  threadleR) always refuses a tie that already exists. Edgelist and matrix
  imports and TSV loading add ties without a per-edge check and remove
  duplicates once at the end, silently keeping the first occurrence (an
  undirected edgelist that lists both `a b` and `b a` gives one tie).
  TSV loading and matrix import used to go through the checked public
  method; they now use the same import route as edgelists. Binary `.bin`
  loading is unchecked, as before: Threadle's own files never contain
  multiedges. `setting(blockmultiedges, …)` now fails as an unknown
  setting.
- **`betweennesscentrality()` is now computed on the binary union of the
  selected layers.** On a 2-mode layer, two nodes are tied if they share at
  least one hyperedge, and the tie counts once. Before, a pair sharing three
  hyperedges acted like three parallel ties, multiplying the shortest paths
  through it. Likewise, with several layers, a tie present in more than one
  selected layer (including a 1-mode tie that also exists as a shared
  hyperedge) now counts once. Results now equal betweenness on a
  materialized binary projection or union, which is what igraph, NetworKit
  and graph-tool give after a standard projection, without ever
  materializing it. Single-layer 1-mode results are unchanged, and so are
  2-mode results where no pair shares more than one hyperedge. How it works:
  the fast per-hyperedge computation is kept, and the double counting is
  subtracted exactly. Once per command, the pairs connected more than once
  are found (stored only for those pairs). In each BFS, their surplus path
  counts are removed as soon as a level is complete, and the matching
  surplus dependency is removed in the backward pass. Verified against
  betweenness on Threadle's own materialized binary projection (identical
  on a 200,000-node network with 1.7 million repeated pairs) and against
  the independent reference in `BetweennessReferenceTests.cs`. Cost:
  sampled betweenness with 100 sources took 5.2 s instead of 4.5 s on a
  random 382,000-node 2-mode network (few repeated pairs), and 4.0 s
  instead of 2.7 s, with about 50 MB more memory, on the repeat-heavy
  200,000-node network.
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

- **Much faster `shortestpath()`/`shortestpaths()`, identical results.** The
  bidirectional BFS now stops at the first node reached from both sides,
  instead of finishing that BFS level. This is exact: every meeting found in
  that level has the same length, and the first one found was already the
  one kept, so distances and returned paths are unchanged. It also stops as
  soon as either side has nothing left to expand: that side has then reached
  everything it can, so there is no path. Before, the other side kept going;
  for a pair involving an isolated node, that meant traversing the whole
  rest of the network. Packed 1-mode layers are read straight from their
  neighbour arrays instead of through a copied array per node. Verified
  against the previous implementation (identical distances and paths on
  24,060 pairs: 1-mode directed and undirected, packed and unpacked,
  multilayer, 2-mode, mixed) and against a plain-BFS reference in the new
  tests. On a 382,000-node projected 1-mode network: 1,000 pairs 20.6 s →
  0.32 s; a single `shortestpath()` call about 70 ms → 0.3 ms.
- **1-mode community detection uses about a third of the memory, identical
  results.** `louvain`, `plm`, `leiden`, `lp` and `lpam` on 1-mode layers
  built their symmetrized working copy of the network as one
  `Dictionary<int, double>` per node, then copied it into arrays while the
  dictionaries were still alive. They now count each node's neighbours
  first and fill the final arrays directly, in the same order and with the
  same weight sums. Packed layers are read straight from their own arrays.
  `plm` also reuses its per-thread working arrays across sweeps, instead of
  allocating new ones for every sweep. Communities and modularity are
  unchanged (verified byte-for-byte across all five methods on directed,
  undirected, valued and multilayer networks with self-ties and sparse node
  ids). Measured peak heap on a 382,000-node projected 1-mode network (97 MB
  as loaded): `louvain` 1.97 GB → 0.61 GB, `plm` 2.17 GB → 0.66 GB, `leiden`
  2.12 GB → 0.74 GB, `lpam` 1.77 GB → 0.43 GB. All four also got faster:
  `louvain` 77 s → 55 s, `plm` 40 s → 23 s, `leiden` 62 s → 45 s, `lpam`
  51 s → 35 s. Most of what remains is needed data: the level-0 working copy
  (kept for the final modularity) plus the first aggregated levels, which
  on a weakly clustered network are still large.
- **Faster `betweennesscentrality()` and `edgebetweennesscentrality()`, on 1-mode and
  2-mode layers.** The Brandes pass no longer stores a predecessor list per
  node or uses dictionaries for distances and path counts: it works on plain
  arrays indexed by the command's compact node numbering, reused per thread,
  and the backward pass finds predecessors by re-scanning neighbours. On a
  2-mode layer, each hyperedge's path counts are now summed once per BFS level
  rather than once per arriving member, so the cost per source drops from up
  to O(Σ hyperedge size²) to O(Σ hyperedge size). (Ties are now counted
  once on the binary union of the selected layers; see Behavior changes.)
  1-mode results are identical to before (verified
  byte-for-byte). 2-mode results can differ only in the last floating-point
  digits (at most about 2e-15 relative), because sums are added in a
  different order. Checked against the previous implementation on 132 cases
  (1-mode directed/undirected, multilayer, valued, packed and unpacked 2-mode,
  mixed 1-mode + 2-mode, sampled) and against a separate textbook Brandes
  reference in the new tests. Sampled `betweennesscentrality()` with 100
  sources (4 cores): 382,000-node random 2-mode network 65.8 s → 5.4 s;
  300,000-node random 1-mode network 52.5 s → 8.4 s; 100,000-node
  scale-free 1-mode network 6.3 s → 1.9 s. GC heap after the run also
  dropped (e.g. 1.46 GB → 74 MB on the 300,000-node network).
- **1-mode community detection without per-node dictionaries, identical
  results.** `louvain`, `leiden`, `lp` and `lpam` on 1-mode layers (and
  `plm`'s aggregation) cleared and refilled a dictionary for every node visit,
  built a dictionary per community when aggregating, and used dictionaries for
  renumbering and the modularity calculation. They now use a weight array plus
  a list of keys in first-seen order, and aggregate by grouping each
  community's members. Keys are visited and weights summed in exactly the same
  order as before, so communities, tie-breaks and modularity are identical
  (verified byte-for-byte across methods, layer types and seeds). Measured on a
  300,000-node random 1-mode network: `louvain` 81.6 s → 70.1 s, `leiden`
  72.8 s → 55.8 s, `lpam` 27.5 s → 22.4 s, `lp` 11.4 s → 9.5 s. Most of
  `louvain`'s remaining time is spent in long tails of local-moving sweeps that
  move only a few nodes each (`plm` caps these at 32 per level).
- **Faster 2-mode community detection, identical results.** `louvain`,
  `leiden`, `lp` and `lpam` on a 2-mode layer built a fresh co-member
  dictionary (plus two arrays) for every node on every sweep, and did a
  dictionary lookup and update per co-member. They now share a node–hyperedge
  incidence built once per call, as plain int arrays, and add shared-hyperedge
  counts per community into one reusable array; degrees are computed in one
  linear pass. Co-members are visited in exactly the same order as before, so
  communities and modularity are unchanged (verified byte-for-byte on saved
  results across seeds, packed and unpacked layers). `louvain` on a
  382,000-node random 2-mode network: 86 s → 35 s.
- **Faster BFS for distance-based commands on 2-mode layers.** `diameter()` (all
  modes), `eccentricity()`, `closenesscentrality()`, `harmoniccentrality()` and
  `shortestpathsattributes()` run one BFS per source node. Each command now
  builds, once, a re-indexed copy of every packed 2-mode layer in the command's
  compact node numbering (node → hyperedges, hyperedge → members, as plain int
  arrays), and each worker thread reuses its distance array, queue and a
  per-hyperedge visited stamp across all its BFS calls — instead of allocating a
  new distance array and hyperedge set per call and doing a dictionary lookup
  per member scanned. Results are unchanged. Measured on random 2-mode test
  networks (4 cores): sampled `diameter()` with 64 sources on 382,000 nodes
  6.4 s → 1.8 s; exact `diameter()` on 38,200 nodes 110 s → 22 s. 1-mode
  layers gain little (closeness on a 30,000-node 1-mode network 54 s → 49 s),
  since their neighbour lookups are unchanged. Extra memory per command: two
  int arrays the size of the layer's affiliation count per packed 2-mode layer
  (≈12 MB for a 1.5M-affiliation layer), plus per thread one distance array,
  one queue and one hyperedge stamp array.
- **Node attribute storage rewritten.** Node membership is now a single
  node-id-to-slot map (replacing the separate HashSet for nodes without
  attributes and Dictionary for nodes with attributes). Sparse attribute
  values are stored as one compact byte array per node (attribute indices
  followed by raw 4-byte values) instead of two `List`s per node, and dense
  attributes as flat per-slot columns. Measured on 1M nodes with 5 attributes
  set on every node: managed memory down from 225 MB to 91 MB (sparse) or
  48 MB (dense), and the script setting them runs 2–3.5× faster. Nodesets
  without any attributes cost about 5 bytes more per node (19 → 23 MB per 1M
  nodes).
- **Binary format version 4**: each attribute definition now carries its
  storage mode; nodes are written once (sorted), followed by sparse values
  per node and dense columns (presence bitmap + values). Version 3 (and
  earlier) files still load, with all attributes as sparse. TSV nodeset
  headers mark dense attributes as `name:Type:dense`; plain `name:Type` headers
  load as sparse, as before.

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

- **Duplicate ties in an imported file now keep the first value.** When an
  edgelist (or a matrix, or a hand-edited TSV file) listed the same tie
  more than once with different values on a valued layer, the value that
  survived duplicate removal was effectively arbitrary for nodes with more
  than 16 ties (the sort used was not stable). On a directed layer, the
  outbound and inbound copies of the tie could even keep different values.
  The first occurrence is now kept, on both copies.
- **Edgelist import no longer adds self-ties to a layer that does not
  allow them.** Such rows are now skipped, as `addedge()` and matrix import
  already did.
- **`randomseed()` ignored a positional seed.** It only read `seed=…`, so
  `randomseed(42)` silently set the default seed (6031769). It now accepts the
  positional form, and rejects a value that isn't an integer instead of
  ignoring it.
- **`setattr()`, `setattrs()`, `removeattr()` and `generateattr()` now mark
  the nodeset as modified.** Previously they changed attribute values
  without setting the flag, so saving a *network* afterwards — which
  re-saves its nodeset only if that nodeset is marked modified — silently
  left those changes out of the nodeset file. Saving the nodeset itself was
  never affected.

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
