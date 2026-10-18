# Changes

## 1.3.1

### New capability on existing commands

> No new commands. Every change below loosens an existing restriction — existing
> 1-mode calls to any of these commands are unaffected.

- **`diameter()`/`eccentricity()`** now accept a 2-mode layer directly, pseudo-projected
  (traversed node-hyperedge-node, without materializing a 1-mode projection).
  `components()` and `shortestpathsattributes()` turn out to already have worked this
  way with no gate at all.
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
  `eigenvectorcentrality()`'s convention) — no `projection=` parameter here, since a
  binary option would need the same global pairwise deduplication this feature exists
  to avoid. `type = 'gn'` (Girvan-Newman) has no 2-mode support: edge betweenness has
  no hyperedge analogue.
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

### Deliberately out of scope

- **`clusteringcoefficient()`/`transitivity()`** stay 1-mode only: projecting a 2-mode
  layer and running the existing clique-counting formula on it would be actively
  *wrong* (every hyperedge becomes a clique, trivially inflating clustering), not
  merely unsupported — a genuinely different two-mode-native measure would be needed
  instead.
- **`coreness()`** stays 1-mode only for now — k-core peeling's interaction with
  pseudo-projected degree on node removal needs its own careful design.
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
