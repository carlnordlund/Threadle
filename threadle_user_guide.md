# Threadle User Guide

---

## 1. Introduction

Threadle is a high-performance software system for building, storing, and querying large,
complex network datasets. It was developed explicitly for working with Swedish full-population
administrative register data — the kind of data held on Statistics Sweden's Mona microdata
server — where the scale and structural complexity of social networks exceed the practical
limits of general-purpose tools.

### Why Not igraph?

igraph is an excellent and widely-used network analysis library, and for many purposes it
remains the right tool. However, it runs into fundamental limitations when working with
population-scale register data:

- **No native multilayer support.** igraph treats a network as a single graph. Representing
  multiple relational domains (co-workers, co-residents, schoolmates) requires maintaining
  separate graph objects and managing node identity across them manually.

- **2-mode data must be projected.** igraph has no native representation for bipartite
  (2-mode) networks as first-class objects. To query whether two persons share a workplace,
  you must first project the bipartite graph into a 1-mode graph — an operation that
  materializes all pairwise co-affiliation edges and can expand memory requirements by
  several orders of magnitude. For a nation of 10 million people and millions of workplaces,
  this projection is simply not feasible in RAM.

- **No native node attribute storage.** Node attributes in igraph are stored as separate
  vectors attached to the graph. At population scale, keeping these in sync with the graph
  and across multiple graph objects becomes cumbersome.

- **Performance limits.** igraph is not designed for networks with hundreds of millions of
  edges across multiple relational layers.

### How Threadle Addresses These Challenges

Threadle was designed from the ground up for the characteristics of register data:

- **Native multilayer architecture.** A Threadle Network can hold multiple relational layers
  simultaneously, each with its own properties (1-mode or 2-mode, directed or undirected,
  binary or valued). All layers share the same Nodeset.

- **Pseudo-projection for 2-mode data.** Threadle stores 2-mode (bipartite) relations as
  true hyperedges — a person is affiliated to a workplace, not connected to every co-worker
  individually. Queries such as "are these two people connected?" or "who are this person's
  co-workers?" work directly on the hyperedge structure, as if the data were projected,
  without ever materializing the projection. This saves enormous amounts of memory.

- **Native typed node attributes.** Attributes (age, sex, region, income category, and so
  on) are stored directly on nodes in the Nodeset, typed (`Int`, `Float`, `Char`, `Bool`,
  `String`), and available for filtering, summarization, and export without external joins.

- **Designed for scale.** Threadle is implemented in .NET/C# with memory-efficient data
  structures optimized for networks with millions of nodes and billions of edges.

### The Threadle Ecosystem

Threadle consists of three components:

- **Threadle Core** — the underlying data model, processing engine, and file I/O library.
- **Threadle CLIconsole** — a command-line interface that exposes Core's functionality
  through a scripting language. On Mona, this runs invisibly in the background.
- **threadleR** — an R package that communicates with CLIconsole and exposes all Threadle
  functionality through familiar R function calls.

On Mona, researchers interact exclusively with threadleR from within R. The CLIconsole is
launched and managed automatically by threadleR. Direct use of the console is not required.

### Funding and Acknowledgements

Threadle was developed within the **The Complete Network of Sweden** research environment,
funded by the Swedish Research Council (Vetenskapsrådet), grant 2024-01861. threadleR was
developed by Yukun Jiao.

### How to Read This Guide

This guide is written primarily for Mona researchers. All worked examples use R and
threadleR. The full CLI command reference is provided in Section 9 for reference, but Mona
users do not need to use CLI commands directly.

---

## 2. Core Concepts

### 2.1 Nodesets and Networks

Every Threadle workflow begins with two fundamental structures: a **Nodeset** and a
**Network**.

A **Nodeset** is the universe of nodes — the set of all individuals (or other entities) in
your dataset. Each node is identified by a unique unsigned integer, which in a register
context is typically an anonymized personal identity number. The Nodeset is also where node
attributes live: demographic variables, derived measures, group memberships, and so on.

A **Network** is the edge structure. It is built on top of a Nodeset and contains one or
more relational **layers**. A Network does not duplicate the nodes — it references the
Nodeset. This means a single Nodeset can underlie multiple Networks (for example, a
full-population network and a regional subnetwork), and removing or filtering nodes in one
does not affect the other.

In practice, for most register workflows you will have one Nodeset and one Network per
project, saved together as a pair of files.

### 2.2 Layers: 1-mode and 2-mode

A **layer** is a relational dimension within a Network. Each layer has a name and a set of
fixed properties defined when the layer is created:

- **Mode**: `1` (unipartite — edges between nodes) or `2` (bipartite — affiliations between
  nodes and hyperedges).
- **Directionality** (1-mode only): directed (asymmetric, e.g. "sent a message to") or
  undirected (symmetric, e.g. "co-authored with").
- **Value type** (1-mode only): `binary` (edge exists or not) or `valued` (edge has a
  numeric weight, e.g. number of interactions).
- **Self-ties** (1-mode only): whether a node can be connected to itself.

2-mode layers do not use these latter three properties — all 2-mode ties are binary by
definition.

In a register data context, typical **1-mode layers** capture direct dyadic relationships
between individuals. A clear example is kinship: parent-child ties, sibling ties, or
marriage ties, all of which are recorded in the Swedish multi-generational register
(FlerGen) and the civil status register (Civilåndr). Note that kinship encompasses several
distinct tie types — parent-child, siblings, spouses — and it is a deliberate research
design decision whether to combine these into a single `kinship` layer or separate them
into `marriages`, `parentchild`, `siblings`, and so on. Separating them allows
layer-specific queries (e.g. "who are this person's siblings?") but increases the number
of layers to manage.

Typical **2-mode layers** capture affiliations: individuals connected to workplaces (via
LISA), residential buildings or properties (via Geo or FastReg), school classes, and so
on. These are stored as hyperedges — each workplace or property is a named hyperedge, and
individuals are affiliated to them.

A Network must have at least one layer before edges or affiliations can be added.

### 2.3 The Pseudo-Projection

The most important concept for researchers coming from igraph is Threadle's handling of
2-mode data.

In a classical bipartite projection, you take a 2-mode network (persons affiliated to
organizations) and compute a 1-mode network (persons connected to persons who share an
organization). This is done by matrix multiplication: the result contains an edge between
every pair of persons who share at least one organization, with edge weights equal to the
number of shared organizations. For a nation of millions of people and hundreds of thousands
of organizations, this projection creates billions of edges and is not feasible to store in RAM.

Threadle takes a different approach. It stores the raw 2-mode structure — persons are
affiliated to named hyperedges (organizations, school classes, households) — and answers
1-mode queries directly from that structure. When you ask "are person A and person B
connected?", Threadle checks whether they share at least one hyperedge, without building the
projected graph. When you ask "who are person A's co-workers?", Threadle finds all persons
affiliated to the same workplaces as person A.

This means that functions like `th_check_edge()`, `th_get_edge()`, and
`th_get_node_alters()` work on 2-mode layers just as they do on 1-mode layers. There is no
need to project before querying. `th_get_edge()` on a 2-mode layer returns the number of
affiliations the two nodes share — exactly the valued projection weight — without
materializing the full projected graph.

When you genuinely need a projected 1-mode layer (for example, to run algorithms that
require it), `th_project_two_mode()` will materialize it. But for most querying and
sampling tasks, the pseudo-projection is sufficient and far more efficient.

### 2.4 Node Attributes

Node attributes are typed variables stored directly on nodes in the Nodeset. Before
importing attribute data, each attribute must be **defined** with a name and type using
`th_define_attr()`. Supported types are:

| Type     | Description                         | Example values         |
|----------|-------------------------------------|------------------------|
| `Int`    | Integer                             | `45`, `0`, `-3`        |
| `Float`  | Floating-point number               | `3.14`, `0.5`          |
| `Char`   | Single character                    | `m`, `f`, `o`          |
| `Bool`   | Boolean                             | `TRUE`, `FALSE`        |
| `String` | Text string                         | `Stockholm`, `retired` |

Attributes can be set individually using `th_set_attr()`, or bulk-imported from a flat file
using `th_import_node_attributes()`.

The flat file format for node attributes is a tab-separated file where the header row
defines attribute names and their types using `attributename:Type` notation. The first
column contains node IDs; its header cell is ignored.

```
[ignored]   gender:Char   age:Int   region:String
100001      m             45        Stockholm
100002      f             38        Göteborg
100003      o             52        Malmö
```

If no type annotation is provided in the header, `String` is assumed. Rows whose node ID
does not exist in the Nodeset are skipped by default (left-join behaviour).

Calculated attributes — such as degree centrality or component membership — are also stored
as node attributes. Functions like `th_degree()` and `th_components()` write their results
directly to the Nodeset as named attributes, which can then be exported or used for
filtering.

### 2.5 File Formats

Threadle uses its own file formats for saving and loading Nodesets and Networks:

| Extension   | Description                                      | Use case                        |
|-------------|--------------------------------------------------|---------------------------------|
| `.tsv`      | Tab-separated text, human-readable               | Inspection, debugging           |
| `.tsv.gz`   | Gzipped TSV                                      | Readable but compressed         |
| `.bin`      | Threadle binary format, compact, not human-readable | Recommended for production   |
| `.bin.gz`   | Gzipped binary                                   | When disk space is critical     |

The `.bin` format is recommended for production use on Mona. Threadle binary files are
already compact: a 10-million-node network with 16 node attributes and several layers
containing millions of edges and affiliations typically occupies around 1.2 GB as a `.bin`
file. Disk space is generally not a constraint on Mona, and `.bin` loads and saves
significantly faster than `.bin.gz`. Use `.bin.gz` only when disk space is genuinely
critical.

A Network file references its associated Nodeset file. When you load a Network, the Nodeset
is loaded automatically — you do not need to load them separately.

**Mutable vs. packed layers.** By default, layers are stored in a mutable format: edges can
be added and removed. Once a layer is finalized and no further modifications are expected,
it can be **packed** using `th_pack()`, which converts it to a more compact, immutable
representation. Packing saves significant RAM for large layers. A packed layer can be
unpacked again with `th_unpack()` if modifications are later needed.

---

## 3. Installation and Setup

### 3.1 Using Threadle on Mona

Threadle is pre-installed on the Mona server. Researchers access it exclusively through the
**threadleR** R package — the CLIconsole runs silently in the background and does not need
to be started or configured manually.

Mona hosts a local copy of the CRAN repository with threadleR included. Install threadleR
once from within your Mona R session:

```r
install.packages("threadleR")
```

This step only needs to be done once. In all subsequent sessions, simply load the package
and start the Threadle process:

```r
library(threadleR)
th_start_threadle()
```

To verify that Threadle is available and ready:

```r
th_is_available()
```

At the end of your session, stop the Threadle process cleanly:

```r
th_stop_threadle()
```

### 3.2 Using Threadle Outside Mona

Researchers who want to use Threadle on their own machine (Windows, macOS, or Linux) need
to install both Threadle itself and threadleR.

#### 3.2.1 Installing Threadle

Two installation options are available at [threadle.dev](https://www.threadle.dev/?page=download):

- **Setup installer** (recommended): Download and run the installer for your operating
  system. This installs Threadle and automatically adds it to your system `PATH`, so
  threadleR can find it without any additional configuration.

- **Standalone executables**: Download the pre-built binary directly. In this case,
  Threadle is not added to `PATH`, and you will need to provide the path to the executable
  when starting threadleR (see below).

Alternatively, build from source via the
[GitHub repository](https://github.com/carlnordlund/Threadle).

#### 3.2.2 Installing and Configuring threadleR

threadleR is not on CRAN. Install it from GitHub using the `remotes` package:

```r
install.packages("remotes")
remotes::install_github("YukunJiao/threadleR")
```

If you installed Threadle using the setup installer (and it is on your `PATH`), start
threadleR without any additional configuration:

```r
library(threadleR)
th_start_threadle()
```

If you downloaded the standalone executable instead, provide the path explicitly:

```r
library(threadleR)
th_start_threadle(path = "/path/to/threadle")   # or "C:/path/to/threadle.exe" on Windows
```

Verify that Threadle is available:

```r
th_is_available()
```

---

## 4. Building a Network from Register Data

### 4.1 Overview of the Pipeline

Threadle reads from files on disk — it does not connect to databases directly. The workflow
for building a network from register data therefore involves writing intermediate flat files
that Threadle imports. This is straightforward in practice and fits naturally into an R
scripting workflow.

The recommended pipeline builds the Nodeset first from a comprehensive register query, then
imports relational layers into the pre-existing Nodeset. This avoids the `addmissingnodes`
pitfall (see Section 4.4.3) and keeps the node universe clearly defined from the outset.

```
1.  SQL query (population + attributes) → R data frame
2.  Write nodeset TSV file with typed header (lopnr, gender:Char, age:Int, ...)
3.  th_start_threadle() + th_sync_wd()
4.  th_load_file("ns", file = "nodeset.tsv", type = "nodeset") → creates Nodeset
5.  th_create_network("net", nodeset = ns) → creates Network on that Nodeset
6.  th_add_layer() for each relational domain
7.  For each layer:
      SQL query → R data frame → write edge/affiliation TSV
      th_import_layer(net, layername = ..., file = ..., addmissingnodes = FALSE)
8.  th_save_file(ns, file = "nodeset.bin")
9.  th_save_file(net, file = "network.bin")
```

For subsequent sessions, the pipeline shortens to:

```
1.  th_load_file("net", file = "network.bin", type = "network")
2.  [query, analyse, extend as needed]
3.  th_save_file(net)
4.  th_stop_threadle()
```

### 4.2 Step 1 — SQL Query to R Data Frame

The first SQL query should define the full node universe — typically all individuals
resident in Sweden in the reference year — and combine it with the node attributes you need.
A natural starting point is the `Geo2017` table (which records geographical data for the
entire Swedish population), joined with background data from `bakgrundsdata` and labour
market variables from `LISA`.

The result of this query becomes the nodeset TSV file: the first column is the anonymized
personal identity number (`lopnr`), and subsequent columns are named with the `name:Type`
notation described in Section 2.4.

```r
library(DBI)
library(odbc)   # standard ODBC driver used on Mona; verify with your Mona project setup

con <- dbConnect(odbc::odbc(), dsn = "Mona")   # DSN name may vary by project

# Nodeset query: all individuals in Sweden with attributes
# Draws on Geo2017, bakgrundsdata, and LISA for geographic and socioeconomic attributes
nodeset_df <- dbGetQuery(con, "
  SELECT g.lopnr,
         b.Kon      AS 'gender:Char',
         b.FodAr    AS 'birthyear:Int',
         g.Lan      AS 'region:String',
         l.Sun2000  AS 'education:String',
         l.DispInk  AS 'income:Int'
  FROM Geo2017 g
  JOIN bakgrundsdata b ON g.lopnr = b.lopnr
  LEFT JOIN LISA2017_Individ l ON g.lopnr = l.lopnr
  WHERE g.ar = 2017
")
# NOTE: Exact column and table names (Kon, FodAr, Lan, Sun2000, DispInk) should be
# verified against the Mona data catalogue for your specific register year and access.
```

Relational layers are then queried separately. For a **1-mode layer** (marriages, from
`Civilåndr2017`):

```r
# Marriage dyads: select currently married pairs where both partners are in the nodeset.
# The Civilåndr register records civil status changes; active marriages are those where
# the most recent event (CivSt) is a marriage record (e.g. CivSt = 'G') and no
# subsequent dissolution (divorce/death) has occurred. Adjust the WHERE clause to
# match the exact coding in your Mona data access agreement.
marriages_df <- dbGetQuery(con, "
  SELECT a.lopnr AS lopnr_ego, b.lopnr AS lopnr_alter
  FROM Civilandr2017 a
  JOIN Civilandr2017 b ON a.MakelopNr = b.lopnr
  WHERE a.CivSt = 'G'
    AND a.lopnr < b.lopnr   -- avoid duplicate pairs (A-B and B-A)
")
# NOTE: Verify exact column names (MakelopNr, CivSt) and filter conditions against
# the Civilåndr documentation for your Mona project.
```

For a **2-mode layer** (workplaces, from `LISA2017_Individ`):

```r
workplace_df <- dbGetQuery(con, "
  SELECT lopnr, ArbStNr
  FROM LISA2017_Individ
  WHERE ar = 2017
    AND ArbStNr IS NOT NULL
")
```

For a **2-mode layer** (residential buildings/properties, from `Geo2017`):

```r
residence_df <- dbGetQuery(con, "
  SELECT lopnr, Fastighet
  FROM Geo2017
  WHERE ar = 2017
    AND Fastighet IS NOT NULL
")
```

```r
dbDisconnect(con)
```

### 4.3 Step 2 — Writing the Flat File

For the nodeset file, the column names in the SQL result already carry the `name:Type`
annotation, so `write_tsv()` with headers produces the correctly formatted file directly:

```r
library(readr)

# Write nodeset TSV — column names from SQL carry the name:Type header
write_tsv(nodeset_df, "nodeset.tsv", col_names = TRUE)
```

For edge/affiliation files, write without headers (Threadle expects bare data by default):

```r
# 1-mode edge list (lopnr_ego, lopnr_alter) — no header
write_tsv(marriages_df, "marriages.tsv", col_names = FALSE)

# 2-mode affiliation list (lopnr, workplace ID) — no header
write_tsv(workplace_df, "workplace.tsv", col_names = FALSE)

# 2-mode affiliation list (lopnr, property ID) — no header
write_tsv(residence_df, "residence.tsv", col_names = FALSE)
```

> **Note on node IDs:** Node IDs must be unsigned integers. Anonymized register IDs
> (`lopnr`) satisfy this requirement directly. Hyperedge names in 2-mode layers are
> strings, so workplace IDs and property IDs can be used as-is.

### 4.4 Step 3 — Importing into Threadle

#### 4.4.1 Creating the Nodeset and Network

Since the nodeset TSV file already contains all nodes and their attributes, the Nodeset is
created by loading the file directly rather than creating an empty one and importing
attributes separately.

`th_load_file()` takes a **variable name** as its first argument — this is the name by
which the structure is known inside the Threadle backend. The return value is an R handle
to that structure. Use short, descriptive names without spaces.

```r
ns  <- th_load_file("ns",  file = "nodeset.tsv", type = "nodeset")
net <- th_create_network("net", nodeset = ns)
```

The optional `name` argument to `th_create_network()` sets a human-readable label stored
in the file header; the first argument (`"net"`) is the backend variable name.

#### 4.4.2 Adding a Layer

Before importing edges, the layer must exist. Define its properties at creation time —
these cannot be changed later without removing and recreating the layer.

```r
# A 1-mode undirected binary layer for marriage ties
th_add_layer(net, layername = "marriages", mode = 1, directed = FALSE, valuetype = "binary")

# A 2-mode layer for workplace affiliations
th_add_layer(net, layername = "workplace", mode = 2)

# A 2-mode layer for residential building affiliations
th_add_layer(net, layername = "residence", mode = 2)
```

Note that other kinship tie types — parent-child, siblings — could each have their own
1-mode layer, or be combined into a single `kinship` layer depending on the research
question. Separate layers allow layer-specific queries; a combined layer simplifies
multi-relational queries across all kinship types at once.

#### 4.4.3 Importing the Edge/Affiliation File

```r
# Import 1-mode edge list (marriages)
th_import_layer(net,
                layername       = "marriages",
                file            = "marriages.tsv",
                format          = "edgelist",
                header          = FALSE,
                addmissingnodes = FALSE)

# Import 2-mode affiliation list (workplaces)
th_import_layer(net,
                layername       = "workplace",
                file            = "workplace.tsv",
                format          = "edgelist",
                header          = FALSE,
                addmissingnodes = FALSE)

# Import 2-mode affiliation list (residential buildings)
th_import_layer(net,
                layername       = "residence",
                file            = "residence.tsv",
                format          = "edgelist",
                header          = FALSE,
                addmissingnodes = FALSE)
```

> **On `addmissingnodes`:** Because the Nodeset was built first from the full population
> register, all expected `lopnr` values are already present. Setting `addmissingnodes =
> FALSE` (the default) means that any `lopnr` appearing in an edge file but not in the
> Nodeset will be silently skipped — which is the correct behaviour here, since it catches
> any data inconsistencies. Only set `addmissingnodes = TRUE` when deliberately building a
> Nodeset from scratch via edge imports rather than from a population query.

For non-standard file layouts, column positions can be overridden using zero-based column
indices. For 1-mode layers use `node1col`, `node2col` (and `valuecol` for valued layers);
for 2-mode layers use `nodecol` and `affcol`:

```r
# Non-standard column order: source in col 0, target in col 1, weight in col 2
th_import_layer(net, layername = "transfers", file = "transfers.tsv",
                format = "edgelist", header = TRUE,
                node1col = 0, node2col = 1, valuecol = 2,
                addmissingnodes = FALSE)

# Non-standard 2-mode file: node ID in col 2, affiliation name in col 0
th_import_layer(net, layername = "workplace", file = "workplace_alt.tsv",
                format = "edgelist", header = TRUE,
                nodecol = 2, affcol = 0,
                addmissingnodes = FALSE)
```

### 4.5 Step 4 — Saving the Threadle File

```r
th_save_file(ns,  file = "nodeset.bin")
th_save_file(net, file = "network.bin")
```

Save the Nodeset first, then the Network. The `.bin` format is recommended — it is fast to
save and load, and population-scale files remain manageable in size. Use `.bin.gz` only if
disk space is a genuine concern.

> **Note:** Saving as `.bin` does **not** update the `nodeset.tsv` text file. If you want
> the TSV to reflect newly computed attributes (e.g. degree centrality added by
> `th_degree()`), you must save it explicitly:
> ```r
> th_save_file(ns, file = "nodeset.tsv")
> ```
> Both `.bin` and `.tsv` can coexist. Reading the TSV into R (see Section 7.2) therefore
> requires that the TSV was saved after the last analysis run.

### 4.6 Complete Worked Example

The following example builds a three-layer network for Sweden 2017: marriage ties (1-mode),
workplace affiliations (2-mode), and residential building affiliations (2-mode), with node
attributes drawn from Geo2017, bakgrundsdata, and LISA.

```r
library(threadleR)
library(readr)
library(DBI)
library(odbc)

# --- Connect to Mona SQL ---
con <- dbConnect(odbc::odbc(), dsn = "Mona")   # verify DSN name with your Mona project

# --- Step 1: Build nodeset from population registers ---
# Combines Geo2017 (geography), bakgrundsdata (background), LISA (labour market)
# Column names carry the name:Type annotation for Threadle
nodeset_df <- dbGetQuery(con, "
  SELECT g.lopnr,
         b.Kon       AS 'gender:Char',
         b.FodAr     AS 'birthyear:Int',
         g.Lan       AS 'region:String',
         l.Sun2000   AS 'education:String',
         l.DispInk   AS 'income:Int'
  FROM Geo2017 g
  JOIN bakgrundsdata b ON g.lopnr = b.lopnr
  LEFT JOIN LISA2017_Individ l ON g.lopnr = l.lopnr
  WHERE g.ar = 2017
")

# --- Step 2: Fetch relational data ---

# 1-mode: marriage ties from Civilåndr2017
marriages_df <- dbGetQuery(con, "
  SELECT a.lopnr AS lopnr_ego, b.lopnr AS lopnr_alter
  FROM Civilandr2017 a
  JOIN Civilandr2017 b ON a.MakelopNr = b.lopnr
  WHERE a.CivSt = 'G'
    AND a.lopnr < b.lopnr
")

# 2-mode: workplace affiliations from LISA2017_Individ
workplace_df <- dbGetQuery(con, "
  SELECT lopnr, ArbStNr
  FROM LISA2017_Individ
  WHERE ar = 2017
    AND ArbStNr IS NOT NULL
")

# 2-mode: residential building affiliations from Geo2017
residence_df <- dbGetQuery(con, "
  SELECT lopnr, Fastighet
  FROM Geo2017
  WHERE ar = 2017
    AND Fastighet IS NOT NULL
")

dbDisconnect(con)

# --- Step 3: Write flat files ---
write_tsv(nodeset_df,   "nodeset.tsv",    col_names = TRUE)   # keep typed header
write_tsv(marriages_df, "marriages.tsv",  col_names = FALSE)
write_tsv(workplace_df, "workplace.tsv",  col_names = FALSE)
write_tsv(residence_df, "residence.tsv",  col_names = FALSE)

# --- Step 4: Start Threadle ---
th_start_threadle()
th_sync_wd()

# --- Step 5: Load nodeset from file, create network ---
# th_load_file() takes a backend variable name as its first argument
ns  <- th_load_file("ns",  file = "nodeset.tsv", type = "nodeset")
net <- th_create_network("net", nodeset = ns)

# --- Step 6: Add layers ---
th_add_layer(net, layername = "marriages", mode = 1, directed = FALSE, valuetype = "binary")
th_add_layer(net, layername = "workplace", mode = 2)
th_add_layer(net, layername = "residence", mode = 2)

# --- Step 7: Import layers ---
th_import_layer(net, layername = "marriages", file = "marriages.tsv",
                format = "edgelist", header = FALSE, addmissingnodes = FALSE)

th_import_layer(net, layername = "workplace", file = "workplace.tsv",
                format = "edgelist", header = FALSE, addmissingnodes = FALSE)

th_import_layer(net, layername = "residence", file = "residence.tsv",
                format = "edgelist", header = FALSE, addmissingnodes = FALSE)

# --- Step 8: Inspect and save ---
th_info(net)

th_save_file(ns,  file = "nodeset.bin")
th_save_file(net, file = "network.bin")

# --- Step 9: Stop Threadle ---
th_stop_threadle()
```

---

## 5. Extending an Existing Network

Once a network has been built and saved, it can be loaded and extended in later sessions —
adding new layers from new register years, supplementing with additional relational domains,
or attaching newly derived node attributes.

### 5.1 Loading a Saved Network

```r
library(threadleR)
th_start_threadle()
th_sync_wd()

net <- th_load_file("net", file = "network.bin", type = "network")
```

The Nodeset is loaded automatically alongside the Network. Use `th_info(net)` to confirm
what layers and attributes are present.

The optional `pack` argument loads layers in their compact immutable form, saving RAM when
no further edge additions are planned:

```r
net <- th_load_file("net", file = "sweden2020.bin.gz", type = "network", pack = TRUE)
```

### 5.2 Adding a New Layer

Adding a new layer follows the same pipeline as the initial build: SQL → flat file →
`th_add_layer()` → `th_import_layer()`. For example, adding a school class layer:

```r
# NOTE: Table and column names for the school register vary by data access agreement.
# Common table names include 'Skolregistret' or 'Elever'; the class identifier may be
# 'KlassId', 'EnhetsId', or similar. Verify with the Mona data catalogue.
school_df <- dbGetQuery(con, "
  SELECT lopnr, klass_id
  FROM Skolregistret
  WHERE ar = 2017
")
write_tsv(school_df, "school.tsv", col_names = FALSE)

th_add_layer(net, layername = "school", mode = 2)
th_import_layer(net, layername = "school", file = "school.tsv",
                format = "edgelist", header = FALSE, addmissingnodes = FALSE)
```

Always use `addmissingnodes = FALSE` when extending an existing network — the node universe
is already defined and should not be silently expanded by new register data.

### 5.3 Adding New Node Attributes

```r
# Example: adding income decile as a new attribute
income_df <- dbGetQuery(con, "
  SELECT lopnr, ink_decil AS income_decile
  FROM inkomstregistret
  WHERE ar = 2020
")

attr_header <- c("lopnr", "income_decile:Int")
write_tsv(income_df, "income.tsv", col_names = FALSE)
writeLines(c(paste(attr_header, collapse = "\t"), readLines("income.tsv")),
           "income.tsv")

th_import_node_attributes(net, file = "income.tsv", addmissingnodes = FALSE)
```

Derived attributes (degree, component membership) computed by Threadle are automatically
added to the Nodeset and can be exported back to R.

### 5.4 Packing Finished Layers

Once a layer is complete and no more edges will be added, pack it to save RAM:

```r
th_pack(net, layername = "marriages")
th_pack(net, layername = "workplace")
th_pack(net, layername = "residence")
```

### 5.5 Saving the Updated Network

```r
th_save_file(ns,  file = "nodeset.bin")
th_save_file(net, file = "network.bin")

th_stop_threadle()
```

---

## 6. Querying a Threadle Network

### 6.1 Inspecting a Network

```r
th_info(net)      # layers, edge counts, node count, attribute definitions
th_preview(net)   # first few nodes/edges from each layer
th_i()            # inventory of all currently loaded structures
```

### 6.2 Node Queries

```r
# Total number of nodes
th_get_nbr_nodes(net)

# Retrieve node IDs (paginated — default limit 1000)
th_get_all_nodes(net, offset = 0, limit = 1000)

# Pick a random node
th_get_random_node(net)

# Get a single attribute value for a node
th_get_attr(net, nodeid = 100001, attrname = "region")

# Get an attribute for multiple nodes at once
th_get_attrs(net, nodes = c(100001, 100002, 100003), attrname = "region")

# Summary statistics for an attribute
th_get_attr_summary(net, attrname = "age")
# Returns mean, median, SD, min, max, Q1, Q3 for numeric types;
# frequency distribution for Char/String; count/missing for all types.
```

### 6.3 Neighbor and Alter Queries

These functions work on both 1-mode and 2-mode layers, using the pseudo-projection for
2-mode layers.

```r
# All alters of a node in a specific layer
th_get_node_alters(net, nodeid = 100001, layernames = "workplace")

# Alters across multiple layers (deduplicated by default)
th_get_node_alters(net, nodeid = 100001,
                   layernames = "workplace;residence",
                   unique = TRUE)

# For directed layers, specify direction
th_get_node_alters(net, nodeid = 100001, layernames = "transfers",
                   direction = "out")   # "in", "out", or "both"

# Sample a random alter — useful in random walk and sampling workflows
th_get_random_alter(net, nodeid = 100001, layernames = "workplace")

# For 2-mode layers: hyperedges a node belongs to
th_get_node_hyperedges(net, layername = "workplace", nodeid = 100001)

# All nodes affiliated to a specific hyperedge
th_get_hyperedge_nodes(net, layername = "workplace", hypername = "org_12345")
```

### 6.4 Edge Queries

```r
# Does an edge (or shared affiliation) exist?
th_check_edge(net, layername = "workplace", node1id = 100001, node2id = 100002)

# Edge value — for 2-mode layers, returns count of shared affiliations
th_get_edge(net, layername = "workplace", node1id = 100001, node2id = 100002)

# Retrieve all edges in a 1-mode layer (paginated)
th_get_all_edges(net, layername = "marriages", offset = 0, limit = 1000)

# Retrieve all hyperedge names in a 2-mode layer (paginated)
th_get_all_hyperedges(net, layername = "workplace", offset = 0, limit = 1000)
```

### 6.5 Degree

`th_degree()` computes degree for all nodes in a layer and stores the result as a named
node attribute. This is the recommended approach for large networks.

```r
# Degree for all nodes — stored as node attribute "deg_marriages"
th_degree(net, layername = "marriages", attrname = "deg_marriages", direction = "both")

# Inspect the degree distribution
th_get_attr_summary(net, attrname = "deg_marriages")

# Degree for a single node
th_get_degree(net, nodeid = 100001, layernames = "marriages", direction = "both")

# Degree across multiple layers (each alter counted once by default)
th_get_degree(net, nodeid = 100001, layernames = "workplace;marriages", unique = TRUE)
```

### 6.6 Components

```r
# Compute component membership — stored as node attribute "comp_marriages"
th_components(net, layername = "marriages", attrname = "comp_marriages")

# How many components, and what are their sizes?
th_get_attr_summary(net, attrname = "comp_marriages")
# The maximum value equals the number of components minus one.
# Each unique value identifies one component.

# Extract a subnetwork for a single component
# th_filter() and th_subnet() require a backend variable name as their first argument
sub_ns  <- th_filter("sub_ns", net, attrname = "comp_marriages", cond = "eq", attrvalue = "0")
sub_net <- th_subnet("sub_net", net, nodeset = sub_ns)
th_get_nbr_nodes(sub_net)
```

> **Note on `th_filter()`:** The `attrvalue` argument is always passed as a string, even
> for numeric attributes. Available conditions are: `"eq"`, `"ne"`, `"gt"`, `"lt"`,
> `"ge"`, `"le"`, `"isnull"`, `"notnull"`.

> **Note on `th_filter()` and `th_subnet()` signatures:** Both functions take a backend
> variable name (a quoted string) as their first argument, followed by the source structure.
> The result is both registered in the Threadle backend under that name and returned as an
> R handle.

### 6.7 Shortest Paths

```r
# Shortest path between two specific nodes
th_shortest_path(net, node1id = 100001, node2id = 100002)

# Restrict to specific layers
th_shortest_path(net, node1id = 100001, node2id = 100002,
                 layernames = "family;workplace")

# All-pairs shortest paths, aggregated by a node attribute category
# WARNING: runs in O(N × (N + E)) time — only feasible for smaller networks or subnetworks
# Requires a backend variable name for the result as the first argument
th_shortest_paths("sp_region", net, attrname = "region")
```

### 6.8 Density

```r
# Exact density for a layer
th_density(net, layername = "marriages")

# Approximate density using sampling (recommended for very large layers)
th_density(net, layername = "workplace", samplesize = 200)
```

### 6.9 Filtering and Subnetworks

```r
# Create a sub-nodeset: all nodes in Stockholm
# First argument is the backend variable name for the new nodeset
sthlm_ns  <- th_filter("sthlm_ns", net, attrname = "region", cond = "eq", attrvalue = "Stockholm")

# Create a subnetwork from that nodeset
# First argument is the backend variable name for the new network
sthlm_net <- th_subnet("sthlm_net", net, nodeset = sthlm_ns)

th_get_nbr_nodes(sthlm_net)
th_density(sthlm_net, layername = "marriages")
```

The filtered sub-nodeset is a deep copy and is independent of the original. The subnetwork
contains only the edges between nodes in the filtered set.

---

## 7. Working with Results in R

### 7.1 A Note on Data Export and Microdata Restrictions

Threadle on Mona operates on microdata — individual-level register data — which is subject
to strict confidentiality rules. Exporting individual-level network data (edge lists, node
lists with identifiers) out of Mona is not permitted under standard Mona access conditions.

This means that functions such as `th_export_layer()` (which exports an edge list) and
`th_export()` (GEXF for Gephi, which is not available on Mona) are **not appropriate for
use with register data on Mona**, except for analysis steps that remain within the Mona
environment. Gephi is not available on Mona.

The correct approach is to use Threadle to compute **aggregate, non-individual measures** —
degree distributions, component statistics, inter-group distances — and store those results
in R data structures. Aggregate findings that contain no individual-level information can
then be exported from Mona in the usual way through the output review process.

One important exception is the random walker methods (`th_rwfpt()`, `th_rwdistances()`):
these produce networks of inter-categorical distances, which are inherently aggregate and
do not contain individual-level data.

### 7.2 Getting Node Measures into R

The most natural way to bring computed node-level measures (degree centrality, component
membership, and other attributes stored on the Nodeset) into R is to read the nodeset TSV
file directly as a data frame. Since the nodeset file was written with a proper header,
it reads cleanly:

```r
nodeset_r <- read_tsv("nodeset.tsv")
```

This gives you a data frame with one row per individual and one column per node attribute,
including any attributes that Threadle has added (degree, component, etc.) after the
network was built and saved. Because the nodeset for the full Swedish population may contain
over 10 million rows, this data frame will be large — but it is straightforward to work
with using standard R tools (`dplyr`, `data.table`) for aggregation, modelling, and
visualization.

> **Note:** After running analyses such as `th_degree()` or `th_components()`, save the
> nodeset **as a TSV** before reading it into R, so the derived attributes are written to
> the text file. Saving as `.bin` does not update the `.tsv`:
> ```r
> th_save_file(ns, file = "nodeset.tsv")   # refresh TSV for reading into R
> th_save_file(ns, file = "nodeset.bin")   # save binary for future Threadle sessions
> nodeset_r <- read_tsv("nodeset.tsv")
> ```

### 7.3 Sampling Node Measures Inside Threadle

For exploring distributions without loading the full nodeset into R, Threadle's random
sampling functions provide an efficient alternative:

```r
# Sample degree values for 10,000 random nodes
sampled_degrees <- replicate(10000, {
  node <- th_get_random_node(net)
  th_get_attr(net, nodeid = node, attrname = "deg_marriages")
})

# Summarize in R
summary(as.numeric(sampled_degrees))
hist(as.numeric(sampled_degrees), main = "Degree distribution (marriages layer, sampled)")
```

This keeps all individual-level data inside Threadle and produces only aggregate R objects.

### 7.4 Projecting 2-mode Layers

When a materialized 1-mode projection is needed for a specific analysis (for example, an
algorithm that requires a standard adjacency structure):

```r
# Valued projection: edge weight = number of shared affiliations
th_project_two_mode(net, layername = "workplace", method = "count")

# Binary projection
th_project_two_mode(net, layername = "workplace", method = "binary")

# Newman-normalized projection (normalizes by hyperedge size)
th_project_two_mode(net, layername = "workplace", method = "newman")
```

The original 2-mode layer is preserved. A new 1-mode layer is created automatically with a
derived name. Note that at population scale, a materialized projection may create an
enormous number of edges and consume substantial RAM — use with care, and prefer the
pseudo-projection for querying where possible.

---

## 8. Practical Considerations on Mona

### Working Directories

Always synchronize Threadle's working directory with R's at the start of a session:

```r
th_sync_wd()
```

Use `th_dir()` to verify the directory contents as seen by Threadle. All file paths in
Threadle commands can be relative to the working directory.

### File Storage

Store flat files (intermediate TSV exports from SQL) and Threadle binary files (`.bin`)
within your allocated Mona project folder. A clear directory structure helps:

```
project/
  data/        # raw TSV files from SQL
  threadle/    # nodeset.bin, network.bin
  output/      # aggregate R results for export
```

### Memory Management

Use `th_info()` to check how much RAM your current structures are consuming — the output
includes memory estimates for the Nodeset and each Network layer. This is useful for
planning whether packing or subnetworking is needed before running memory-intensive
analyses.

- Pack finished layers (`th_pack()`) before running analyses — this converts mutable layers
  to compact immutable storage and can substantially reduce RAM usage.
- Load with `pack = TRUE` when opening a network that you do not plan to modify.
- Use `.bin` for all Threadle files. At population scale, `.bin` files are already compact
  (a 10-million-node network with 16 attributes and several layers typically occupies around
  1.2 GB), and `.bin` loads and saves significantly faster than `.bin.gz`.

### Long-running Imports

Importing population-scale layers (tens of millions of affiliations) takes time. Structure
your build script so that the full import-and-save sequence runs as a single uninterrupted
script. Use `th_load_script()` to run a pre-written CLI script if preferred.

The default timeout for threadleR is 1800 seconds (30 minutes). For very large imports,
increase this before starting:

```r
options(threadle.timeout = 7200)   # 2 hours
th_start_threadle()
```

### Reproducibility

Set a random seed before any stochastic operations (random walkers, random network
generation, sampling):

```r
th_random_seed(seed = 12345)
```

### Session Hygiene

Always stop Threadle cleanly at the end of a session:

```r
th_stop_threadle()
```

---

## 9. Reference

### 9.1 threadleR Function Reference

The table below lists all exported functions in threadleR, verified against the package
`NAMESPACE` and source (`R/threadle_client.R`). Run `getNamespaceExports("threadleR")` or
`ls("package:threadleR")` for the definitive live list in your installed version.

**Note on `th_export_node_attributes`:** No such function exists. Node attributes are
imported with `th_import_node_attributes()`; they are read back into R by saving the
nodeset as a TSV and reading it with `read_tsv()` (see Section 7.2).

#### Session Management

| Function | Arguments | Description |
|---|---|---|
| `th_start_threadle()` | `path = NULL` | Launch the Threadle CLI process |
| `th_stop_threadle()` | — | Terminate the Threadle process |
| `th_sync_wd()` | — | Sync Threadle working directory to R's `getwd()` |
| `th_is_available()` | `path = "threadle"` | Check if the Threadle executable is findable |
| `th_i()` | — | Inventory of all currently loaded structures |
| `th_cmd()` | `cmd, args = list()` | Low-level escape hatch for unwrapped CLI commands |
| `th_stage_examples_to_wd()` | `folder = "threadle_examples", overwrite = TRUE` | Copy bundled example files to working directory |

#### Structure Management

| Function | Arguments | Description |
|---|---|---|
| `th_create_nodeset()` | `var, name = NULL, createnodes = 0` | Create an empty nodeset |
| `th_create_network()` | `var, nodeset, name = NULL` | Create a network from a nodeset |
| `th_load_file()` | `name, file, type, pack = FALSE` | Load nodeset or network from file |
| `th_save_file()` | `structure, file = ""` | Save nodeset or network to file |
| `th_load_examples()` | `examples = c("mynet", "lazega")` | Load bundled example networks |
| `th_delete()` | `structure` | Delete a structure |
| `th_delete_all()` | — | Delete all structures |
| `th_info()` | `structure` | Metadata: layers, counts, attributes |
| `th_preview()` | `structure` | Preview first few nodes/edges |

#### Layer Management

| Function | Arguments | Description |
|---|---|---|
| `th_add_layer()` | `network, layername, mode, directed = FALSE, valuetype = c("binary", "valued"), selfties` | Add a layer |
| `th_remove_layer()` | `network, layername` | Remove a layer and all its edges |
| `th_clear_layer()` | `network, layername` | Remove all edges from a layer |
| `th_pack()` | `network, layername = NULL` | Convert to compact immutable storage |
| `th_unpack()` | `network, layername = NULL` | Convert back to mutable storage |

#### Data Import and Export

| Function | Arguments | Description |
|---|---|---|
| `th_import_layer()` | `network, layername, file, format, node1col = 0, node2col = 1, valuecol = 2, nodecol = 0, affcol = 1, header = FALSE, sep = "\t", addmissingnodes = FALSE` | Import edges from flat file |
| `th_import_node_attributes()` | `structure, file, addmissingnodes = FALSE, sep = "\t"` | Import node attributes from TSV |
| `th_export_layer()` | `network, layername, file, format = "edgelist", header = TRUE, sep = "\t"` | Export layer to flat file |
| `th_export()` | `network, format = "gexf", file, layername` | Export to external format (GEXF; not available on Mona) |

#### Node Operations

| Function | Arguments | Description |
|---|---|---|
| `th_add_node()` | `structure, nodeid` | Add a single node |
| `th_remove_node()` | `structure, nodeid` | Remove a node and all its edges |
| `th_define_attr()` | `structure, attrname, attrtype = c("int","char","float","bool","string")` | Define a node attribute |
| `th_undefine_attr()` | `structure, attrname` | Remove an attribute definition |
| `th_set_attr()` | `structure, nodeid, attrname, attrvalue` | Set a single attribute value |
| `th_remove_attr()` | `structure, nodeid, attrname` | Remove an attribute value from a node |
| `th_get_attr()` | `structure, nodeid, attrname` | Get a single attribute value |
| `th_get_attrs()` | `structure, nodes, attrname` | Get attribute values for multiple nodes |
| `th_get_attr_summary()` | `structure, attrname` | Summary statistics for an attribute |
| `th_generate_attr()` | `structure, attrname, attrtype = c("int","float","bool","char","string")` | Generate random attribute values |
| `th_filter()` | `name, nodeset, attrname, cond, attrvalue = NULL` | Create filtered sub-nodeset |

#### Edge and Affiliation Operations

| Function | Arguments | Description |
|---|---|---|
| `th_add_edge()` | `network, layername, node1id, node2id, value = 1, addmissingnodes = TRUE` | Add a 1-mode edge |
| `th_remove_edge()` | `network, layername, node1id, node2id` | Remove a 1-mode edge |
| `th_add_hyper()` | `network, layername, hypername, nodes = c()` | Add a hyperedge |
| `th_remove_hyper()` | `network, layername, hypername` | Remove a hyperedge |
| `th_add_aff()` | `network, layername, nodeid, hypername, addmissingnode = TRUE, addmissinghyperedge = TRUE` | Add a node–hyperedge affiliation |
| `th_remove_aff()` | `network, layername, nodeid, hypername` | Remove an affiliation |
| `th_check_edge()` | `network, layername, node1id, node2id` | Check if edge or shared affiliation exists |
| `th_get_edge()` | `network, layername, node1id, node2id` | Get edge value (2-mode: shared affiliation count) |
| `th_get_all_edges()` | `network, layername, offset = 0, limit = 1000` | All edges in a 1-mode layer (paginated) |
| `th_get_all_hyperedges()` | `network, layername, offset = 0, limit = 1000` | All hyperedge names in a 2-mode layer (paginated) |
| `th_get_random_edge()` | `network, layername, maxattempts = 100` | Sample a random edge |

#### Node Queries

| Function | Arguments | Description |
|---|---|---|
| `th_get_nbr_nodes()` | `structure` | Total node count |
| `th_get_all_nodes()` | `structure, offset = 0, limit = 1000` | All node IDs (paginated) |
| `th_get_random_node()` | `structure` | Sample a random node ID |
| `th_get_nodeid_by_index()` | `structure, index` | Node ID at a given index position |
| `th_get_node_alters()` | `network, nodeid, layernames = "", direction = "out", unique = TRUE` | All alters of a node |
| `th_get_random_alter()` | `network, nodeid, layernames = "", direction = "both", balanced = FALSE, weighted = FALSE` | Sample a random alter |
| `th_get_node_hyperedges()` | `network, layername, nodeid` | Hyperedges a node belongs to (2-mode) |
| `th_get_hyperedge_nodes()` | `network, layername, hypername` | Nodes in a hyperedge (2-mode) |
| `th_get_degree()` | `network, nodeid, layernames = NULL, direction = "out", unique = TRUE` | Degree for a single node |

#### Analysis

| Function | Arguments | Description |
|---|---|---|
| `th_degree()` | `network, layername, attrname = NULL, direction = "out"` | Degree for all nodes → node attribute |
| `th_components()` | `network, layername, attrname = NULL` | Component membership → node attribute |
| `th_density()` | `network, layername, samplesize = 200` | Layer density |
| `th_shortest_path()` | `network, node1id, node2id, layernames = NULL` | Shortest path between two nodes |
| `th_shortest_paths()` | `name, network, attrname, layernames = NULL` | All-pairs shortest paths by attribute (small networks only) |
| `th_project_two_mode()` | `network, layername, method = c("count", "newman", "binary")` | Materialize 2-mode projection |
| `th_symmetrize()` | `network, layername, method = c("max", "min", "minnonzero", "average", "sum", "product")` | Symmetrize a directed 1-mode layer |
| `th_dichotomize()` | `network, layername, cond = c("ge","eq","ne","gt","lt","le")` | Dichotomize a valued layer |
| `th_subnet()` | `name, network, nodeset` | Create subnetwork from a nodeset |
| `th_generate()` | `network, layername, type, p = NULL, k = NULL, beta = NULL, m = NULL, h = NULL, a = NULL` | Generate a random network (ER, WS, BA, 2-mode) |
| `th_rwdistances()` | `name, network, attrname, maxsteps, layernames = NULL, walkfactor = 1.0, balanced = FALSE, weighted = FALSE, backtrack = FALSE, savesteps = FALSE` | Random walker distance measure |
| `th_rwfpt()` | `name, network, attrname, maxsteps, layernames = NULL, walkfactor = 1.0, minpairobs = 10L, balanced = FALSE, weighted = FALSE, return_histograms = FALSE` | First-passage-time distance measure |

#### Utilities

| Function | Arguments | Description |
|---|---|---|
| `th_dir()` | `path = NULL` | List directory contents |
| `th_set_workdir()` | `dir` | Set Threadle working directory |
| `th_get_workdir()` | — | Get current Threadle working directory |
| `th_sync_wd()` | — | Sync to R working directory |
| `th_random_seed()` | `seed = 6031769L` | Set random seed for reproducibility |
| `th_load_script()` | `file` | Execute a CLI script file |
| `th_setting()` | `name, value` | Toggle a Threadle internal setting |

---

### 9.2 CLI Command Quick Reference

The following table lists all Threadle CLI commands. Mona researchers do not need to use
these directly — all functionality is available through the threadleR functions in Section
9.1. The CLI reference is provided for completeness and for users running Threadle outside
Mona. Full specifications for each command are available at
[threadle.dev/cli](https://www.threadle.dev/?page=cli).

| Command | Description |
|---|---|
| **Session** | |
| `exit` | Exit the CLIconsole |
| `about()` | Display version and metadata |
| `help()` | List all commands or get help on a specific command |
| `i()` | Inventory of loaded structures |
| **Structure management** | |
| `createnodeset()` | Create an empty nodeset |
| `createnetwork()` | Create a network from a nodeset |
| `loadfile()` | Load nodeset or network from file |
| `savefile()` | Save nodeset or network to file |
| `delete()` | Delete a structure |
| `deleteall()` | Delete all structures |
| `info()` | Metadata about a structure |
| `preview()` | Preview nodes/edges |
| **Layer management** | |
| `addlayer()` | Add a layer |
| `removelayer()` | Remove a layer |
| `clearlayer()` | Remove all edges from a layer |
| `pack()` | Convert layer to compact immutable storage |
| `unpack()` | Convert layer back to mutable storage |
| **Data import/export** | |
| `importlayer()` | Import edges from flat file |
| `importnodeattributes()` | Import node attributes from TSV |
| `exportlayer()` | Export layer to flat file |
| `export()` | Export to external format (GEXF) |
| **Node operations** | |
| `addnode()` | Add a node |
| `removenode()` | Remove a node |
| `defineattr()` | Define a node attribute |
| `undefineattr()` | Remove an attribute definition |
| `setattr()` | Set an attribute value |
| `removeattr()` | Remove an attribute value |
| `getattr()` | Get an attribute value |
| `getattrs()` | Get attribute values for multiple nodes |
| `getattrsummary()` | Summary statistics for an attribute |
| `generateattr()` | Generate random attribute values |
| `filter()` | Create filtered sub-nodeset |
| **Edge and affiliation operations** | |
| `addedge()` | Add a 1-mode edge |
| `removeedge()` | Remove a 1-mode edge |
| `addhyper()` | Add a hyperedge |
| `removehyper()` | Remove a hyperedge |
| `addaff()` | Add a node–hyperedge affiliation |
| `removeaff()` | Remove an affiliation |
| `checkedge()` | Check if edge or shared affiliation exists |
| `getedge()` | Get edge value |
| `getalledges()` | All edges in a 1-mode layer |
| `getallhyperedges()` | All hyperedge names in a 2-mode layer |
| `getrandomedge()` | Sample a random edge |
| **Node queries** | |
| `getnbrnodes()` | Node count |
| `getallnodes()` | All node IDs |
| `getrandomnode()` | Random node ID |
| `getnodeidbyindex()` | Node ID by index |
| `getnodealters()` | Alters of a node |
| `getrandomalter()` | Random alter |
| `getnodehyperedges()` | Hyperedges a node belongs to |
| `gethyperedgenodes()` | Nodes in a hyperedge |
| `getdegree()` | Degree for a single node |
| **Analysis** | |
| `degree()` | Degree for all nodes |
| `components()` | Component membership |
| `density()` | Layer density |
| `shortestpath()` | Shortest path between two nodes |
| `shortestpaths()` | All-pairs shortest paths |
| `projecttwomode()` | Materialize 2-mode projection |
| `symmetrize()` | Symmetrize a directed layer |
| `dichotomize()` | Dichotomize a valued layer |
| `subnet()` | Create subnetwork |
| `generate()` | Generate random network |
| `rwdistances()` | Random walker distance measure |
| `rwfpt()` | First-passage-time distance measure |
| **Utilities** | |
| `dir()` | List directory contents |
| `setwd()` | Set working directory |
| `getwd()` | Get working directory |
| `randomseed()` | Set random seed |
| `loadscript()` | Execute a CLI script |
| `setting()` | Toggle an internal setting |

---

### 9.3 File Format Reference

#### Nodeset TSV Format

The nodeset flat file is a tab-separated file. The first row is a header where each column
(after the first) defines an attribute using `attributename:Type` notation. The first column
contains node IDs; its header cell is ignored. Supported types: `Int`, `Float`, `String`,
`Bool`, `Char`. If no type is specified, `String` is assumed.

```
[ignored]   gender:Char   age:Int   income_decile:Int   region:String
100001      m             45        7                   Stockholm
100002      f             38        4                   Göteborg
100003      o             52        9                   Malmö
```

#### Edge List Format (1-mode)

Two columns: source node ID, destination node ID. For valued layers, a third column holds
the edge weight. No header by default; set `header = TRUE` in `th_import_layer()` if one is
present. Tab-separated by default.

```
100001  100002
100001  100003
100003  100004
```

#### Edge List Format (2-mode)

Two columns: node ID, hyperedge name. Hyperedge names are strings. No header by default.

```
100001  org_12345
100002  org_12345
100002  org_67890
```

#### Binary Format

The `.bin` and `.bin.gz` formats are Threadle's own compact binary formats. They are not
human-readable and should not be edited externally. Use `.tsv` or `.tsv.gz` for
human-readable exports. `.bin` is recommended for production storage — it is fast to load
and save, and population-scale files remain manageable in size. Use `.bin.gz` only when
disk space is genuinely critical.

---

### 9.4 Glossary

**Affiliation.** A membership relation between a node and a hyperedge in a 2-mode layer.
Stored directly; not derived from projection.

**Bipartite network.** A network with two distinct sets of nodes, where edges connect only
across sets (e.g. persons and organizations). Represented in Threadle as a 2-mode layer.

**Hyperedge.** A named entity in a 2-mode layer representing a shared context — a workplace,
school class, household, or event. Nodes are affiliated to hyperedges, not to each other
directly.

**Layer.** A relational dimension within a Network, with fixed properties (mode,
directionality, value type). A Network can contain multiple layers.

**Mutable/immutable layer.** A mutable layer supports adding and removing edges. An
immutable (packed) layer is stored more compactly but cannot be modified. Use `th_pack()`
and `th_unpack()` to convert between them.

**Network.** The edge structure in Threadle, built on top of a Nodeset and containing one
or more layers.

**Nodeset.** The universe of nodes in Threadle, each identified by a unique unsigned integer
ID. Node attributes are stored in the Nodeset.

**Pseudo-projection.** Threadle's ability to query 2-mode data as if it were projected to
1-mode, without materializing the projected graph. Functions like `th_check_edge()`,
`th_get_edge()`, and `th_get_node_alters()` use the pseudo-projection transparently.

**Projection.** A derived 1-mode network computed from a 2-mode network, where two nodes
are connected if they share at least one affiliation. Threadle can materialize this with
`th_project_two_mode()`, but pseudo-projection is usually sufficient for querying.

**Unipartite network.** A network where all nodes belong to the same set and edges connect
nodes to other nodes. Represented in Threadle as a 1-mode layer.
