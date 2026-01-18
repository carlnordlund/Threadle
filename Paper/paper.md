---
title: "Threadle: A high-performance, memory-efficient data management system for
  large, multilayer and multimode networks"
tags:
- networks
- C\#/.NET
- multilevel networks
- multimode networks
date: "2025-11-21"
output:
  pdf_document: default
  word_document: default
license: MIT
repository: https://github.com/carlnordlund/Threadle
affiliations:
- name: The Institute for Analytical Sociology, Department of Management and Engineering,
    Linköping University, Norrköping, Sweden
  index: 1
authors:
- name: Carl Nordlund
  corresponding: true
  orcid: "0000-0001-5057-1985"
  affiliation: 1
- name: Yukun Jiao
  orcid: "0009-0009-4826-4682"
  affiliation: 1
bibliography: paper.bib
---
# Summary
**Threadle** ([website](https://www.threadle.dev)) is an open-source, high-performance and memory-efficient network storage and query engine written in C#/.NET. Designed specifically for working with full-population networks derived from Swedish administrative register data, this representing a very large, multi-layer, mixed-mode and heterogeneous node attribute network with millions of nodes and several billions of edges, Threadle’s general design principles are also applicable to other large, feature-rich relational datasets in need of memory-efficient storage. Unlike existing general-purpose libraries that emphasize analytical algorithms, Threadle is explicitly focused on efficient storage, fast neighborhood retrieval, compact representation of multilevel and multimodal relationships, and scalable handling of node attributes, providing a backend to the implementation of sample- and traversal-based analytical procedures at the frontend level.

Threadle natively supports multilayer structures, with memory-minimized representations of relational data stored in respective layers according to their specific properties. Such layers can be either 1-mode (unipartite) or 2-mode (bipartite), where a core innovation in Threadle is how 2-mode layers utilize hyperedges and the efficient querying of such layers as if they were projected to 1-mode data, though without doing such memory-intensive projections. Threadle has an internal memory-efficient system for storing node attributes of four different data types, optimized for the heterogeneous distribution of different types of node attributes, i.e. where it is expected that certain node attributes are only eligible for a subset of nodes. For file storage, Threadle offers both a human-readable text format as well as a binary file format, both which can be Gzip-compressed for additional file memory savings, and external data can be imported in both edgelist and matrix formats.
Apart from the core functionality related to storage, management and querying of network data, Threadle also implements methods and commands for both processing, data generation, and, to a lesser extend, network analysis. Four customizable random network generators are available: Erdös-Renyi, Watts-Strogatz, Barabasi-Albert, as well as functionality to generate random hyperedge networks, and it is also possible to generate uniformly distributed random attributes. Although working efficiently for large networks, methods for network density, degree centrality, and pair-wise shortest path are perhaps best seen as proof-of-concepts and templates for future implementations of analytical functionality. Threadle also offers methods and commands for attribute-based filtering of nodesets, the extraction of network subsets, and methods for the dichotomization and symmetrization of relational layers.

Whereas all relevant functionality is found in the Threadle.Core module, a command-line interface frontend console is also provided: Threadle.CLIconsole. Using its own scripting language for easy human-readable interaction, the CLI console can also be run in JSON mode for easy interoperability with other languages. One such JSON-utilizing frontend is provided: threadleR. This library allows for working with Threadle directly from R, enabling researchers to run sampling- and traversal-based workflows on very large, multilayer, mixed-mode and heterogeneous networks directly from R.

# Statement of need
Large-scale administrative register data – such as the Swedish population registers for research – are extremely high-volume and structurally complex, containing multiple relational layers (kinship, residence, employment, education etc.), many of which are two-mode (affiliation) structures, for a complete country population spanning several decades. Many research applications involving multilayer random-walker models, simulation-based methods, and repeated sampling of ego networks across layers require very fast retrieval of neighboring alters and node attributes, while keeping a memory footprint small enough to allow population-scale networks, possibly for multiple years, to be simultaneously loaded into RAM.

General-purpose libraries such as igraph (Csardi and Nepusz 2006), networkx (Hagberg, Schult, and Swart 2008), NetworKit (Staudt et al 2015), graph-tool (Peixoto, 2017) offer extensive analytical toolsets, but their internal data models are not designed to support the structural and memory requirements of multilayer and multimode data found in full-population register data. Specifically, in these existing general-purpose libraries:
- Multilayer networks must typically be represented by attaching attributes to edges, storing these in R or Python rather than in the engine itself, with substantial extra memory overhead.
- Relations are typically stored as unipartite graphs, i.e. where there is a single global collection of node pairs (i.e. an edgelist) to represent all relations across all layers and edge types as 1-mode relations.
- Two-mode (affiliation; bipartite) data are commonly projected to their one-mode form, expanding each affiliation of k nodes into k² pair-wise connecting edges, which memory-wise can be prohibitively expensive for large bipartite datasets.
- Node attributes are often stored in general-purpose metadata containers (e.g., dictionaries and data frames) in R or Python, rather than the graph engine itself, making attribute-intensive queries and management slow and/or memory-heavy.
- Repeated sampling workflows involving ego-network intersections of multilayer transitions rapidly exhaust memory during projection or repeated reconstruction.

Faced with the specific shortcomings of these otherwise excellent alternative systems for managing and analyzing network data, Threadle was developed to provide a dedicated backend for representation and querying of full-population, feature-rich networks, with multiple relational layers of different properties and modes and a large range of node attributes. As such, Threadle is not an alternative interface to igraph or networkx, or an alternative network-analytical framework. It is, however, an alternative storage and query layer for large, feature-rich networks. Analytical algorithms (e.g., random walks, sampling routines, regression models applied to structural outcomes) are intended to be implemented externally, typically in R, using Threadle for the underlying network access.

# Design and key features
- **1.	Native multilayer support**
  Networks consist of multiple layers, each defined as either a 1-mode or 2-mode relational structure. For 1-mode layers, relations can be either binary or valued, and either symmetric or directional, whereas 2-mode networks store all affiliations as hyperedges. Layers can be queried independently or jointly with constant-time neighbor lookups and edge checking, agnostic of layer mode.
- **2.	Node-distributed edgesets**
  Within each layer, each node owns its own relations to its neighbors, providing:
  - Layer-specific edge types (directional or symmetric, binary or valued)
  - Optimal storage solution adapted to the specific edge type
  - Constant-time neighbor (alter) retrieval
  - Option to only store outbound edges for directional layers, halving memory usage (suitable for traversal-based heuristics on layers with directional edges)
- **3.	Pseudo-projected two-mode storage via hyperedges**
Threadle stores 2-mode data as collections of hyperedges rather than projecting them into dense 1-mode projections. This avoids generating potentially millions of edges while still enabling fast, hash-based pseudo-1-mode queries:
  - Whether two nodes are connected (share at least one affiliation)
  - How many affiliations they share
  - Constant-time neighbor retrieval for 2-mode layers
    This data storage design drastically reduces memory footprint while preserving full query capabilities.
- **4.	Integrated, memory-efficient node attributes**
  Threadle supports four node attribute types: integers, floating point, characters, and booleans. Defining an attribute costs no memory until a value is assigned to a particular node. A compact internal scheme distinguishes between nodes with and without attributes, eliminating the need for any pre-allocation overhead.
- **5.	Explicit memory optimization with low time complexity**
  Data structures are optimized to minimize bytes per node, per relation, and per attribute, while preserving O(1) or O(n) access patterns. Internal user settings allow customization of storage heuristics for directional edges and cached node access.
- **6.	Modular design: Core + CLI frontend**
  - Threadle.Core implements all data models, storage structures, processors, methods, and file I/O. This also contains functionality for random network generation and some baseline analytical functions (e.g. degree centrality, network density, pair-wise shortest path algorithm)
  - Threadle.CLIconsole is a console/terminal frontend exposing core functionality through its own scripting language. Runs in default human-readable text mode by default, but CLIconsole can also be run in JSON mode, suitable for interoperability with R, Python and other languages and systems.
  - threadleR is an R library/package providing full interoperability with Threadle from R. Providing a set of corresponding R functions for the various Threadle commands, threadleR provides an igraph-like environment for working with Threadle.
- **7.	Cross-platform availability**
  Threadle is written in C#/.NET using standard NET8.0 libraries and provides precompiled,self-contained binaries for Windows, Linux, and macOS. Being open source, Threadle can also be compiled directly from source, using the build instructions available on its github repository.

# Research impact
(Mention how we intend to use this to work with full-population register data: opens up brand new possibilities to address all of the research questions we have set up to do. Traversal-based analyses: random walkers, sampling, domain overlap etc.)
(As of now: not used; novel product. Due to the sensitivity of data intended to work with, located on secure servers, provide it as open source with full transparency)
(Spread to other similar full-population research groups around Europe)

# Usage Example
(Exemplify network handling, e.g. generate network with multiple layers and two types of random networks. Explore shortest paths: singular layer, or all. Calculate degree centrality and density.)
(Generate layer for 2-mode data, mention how many 1-mode edges this would correspond to: explore shortest paths there as well)
(Exemplify use patterns in threadleR: importing example network, building a simple traversal engine, saving in binary format)

# Software Design
(Rewrite: describe software architecture/design more thoroughly. Mention where extensions should go.)
(First: describe Core. Start with data types: Network and Nodeset objects: fundamental types. Explain from there: e.g. node attributes, layers etc.)
(Provide class diagram etc)
(Mention future frontends: also that Threadle.Core.dll can be used directly by other systems)

# Use of AI statement
AI assistance (Claude code) was occasionally used as an external reviewer and stress-tester of some architectural design choices, as well as knowledge-related discussions (e.g., how to structure InnoSetup-files, understanding the need for buffers when decompressing a gzip stream, and the parametrization of Watts-Strogatz random graph generation). Claude as well as the Microsoft Word readability tool were occasionally used for proofreading and to improve readability of the manuscript text.

# Acknowledgements
Threadle was developed within the research environment The Complete Network of Sweden (NetReg, netreg.se), funded by the Swedish Research Council (grant 2024-01861).

# References
