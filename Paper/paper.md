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
**Threadle** ([website](https://www.threadle.dev)) is an open-source, high-performance system for storing and querying large multilayer and multimode networks. Implemented in C#/.NET, it provides a compact in-memory representation of heterogeneous relational structures and enables fast query operations - neighborhood lookups, random walker traversals, node attribute retrieval, and similar access patterns - required for simulation-based and sampling-intensive workflows. Threadle supports both one-mode and two-mode layers, represents affiliation structures using hyperedges, and stores node attributes in a memory-efficient internal format. The package includes a console interface and an R interoperability library (**threadleR**), making it suitable as a backend for large-scale network analysis pipelines.

# Statement of Need
Population-scale administrative registers and other large relational datasets often include multiple network layers—kinship, residence, schooling, workplaces, and other affiliations—many of which are bipartite and attribute-dense. Analyses involving random walks, multilayer diffusion processes, or repeated ego-network sampling require rapid access to neighbors and attributes while keeping memory usage low enough to maintain full-population networks, preferably across multiple years, in RAM.

General-purpose graph libraries such as **igraph** (Csardi & Nepusz 2006) and **networkx** (Hagberg et al. 2008) offer extensive analytical functionality but are not designed for these structural and memory demands. Their data models lack native multilayer abstractions, bipartite networks typically require projection to one-mode form, and node attributes are stored in high-overhead R/Python containers. These limitations make large, multilayer, multimode networks challenging or infeasible to store and query efficiently.

Threadle addresses this gap by providing a dedicated storage and query engine optimized for heterogeneous, feature-rich networks. Designed specifically with full-population administrative register data in mind, Threadle's general design principles and patterns are equally applicable to other, similarly complex relational dataset. It is intended not as an alternative analytics framework but as a backend data layer supporting workflows and analytical heuristics implemented in external environments such as R.

# Software Description

## Data Model
Threadle separates between two types of structures: networks and nodesets. Each network has a  reference to a specific nodeset, but nodesets can be handled and managed as independent objects. Following general separation of concerns design principles, networks manage the set of relational layers in a network, and nodesets manage node attributes. Similar to **igraph**, each node is represented by a unique id in the form of an unsigned integer.

Relational layers in Threadle are either one-mode or two-mode.  
- **One-mode layers** may be binary or valued, symmetric or directional.  
- **Two-mode layers** use hyperedges to represent affiliations, enabling fast queries such as neighbor retrieval and checking edges (i.e. the number of shared affiliations two nodes have) without generating the O(k²) edges typical of projected bipartite graphs.

Node attributes (integers, floats, characters, booleans) are stored in a compact, allocation-aware scheme in which an attribute consumes memory only when assigned. As nodesets manage their nodal attributes, a nodeset corresponds to a standard data frame/table.

## Performance Model
Threadle stores each node’s relations in node-owned edgesets in each layer, enabling:  
- constant-time neighborhood retrieval, edge-, and node querying,  
- layer-specific storage optimizations,  
- optional storage of outbound-only edges for directional layers, reducing memory requirements.  

Internal data structures are tuned for low per-element memory usage while maintaining O(1) or O(n) access patterns, making Threadle suitable for workflows that repeatedly retrieve neighborhoods, test node adjacency, traverse relational layers, or sample alters, coupled with a memory-efficient internal node attribute management system. 

## Interfaces
The software consists of two components:  
- **Threadle.Core**, containing all data models, storage structures, basic method libraries, and file I/O;  
- **Threadle.CLIconsole**, a lightweight command-line interface and variable space exposing Core operations, and serving as the backend for the R package **threadleR**.  

Precompiled binaries are available for Windows, Linux, and macOS at the project website ([Threadle.dev](https://www.threadle.dev)), and the source code allows for direct incorporation of Threadle.Core into other software.

# Comparison with Existing Tools
Existing large-scale network analysis libraries such as NetworKit [ref] and mlnlib store networks using adjacenty-based graph representations in which relations are encoded as pairwise edges. NetworKit implements an adjacency-array graph structure with O(n+m) memory complexity, where each edge is defined by a pair of nodes, and node and edge attributes are kept in external containers addressable by indices. mlnlib similarly represents multilayer networks using a sparse adjacency matrix, encoding layer membership directly in the values associated with pairwise edges via a binary (power-of-two) scheme.

In both cases, network structure is stored as one-mode adjacency data, which requires bipartite/affiliation relations to be projected and expressed as edges, i.e. pairs of nodes. Threadle instead stores layers with two-mode data directly as hyperedges, enabling identical queries at competitive speeds while avoiding the memory-consuming construction and storage of projected edge sets. Thus, instead of representing and storing an individual affiliation with N nodes in the form of N*(N-1)/2 edges, i.e. a set of N*(N-1) nodes, Threadle stores this affiliation as a set of N nodes.

# Acknowledgements
Threadle was developed within the research environment **The Complete Network of Sweden ([netreg.se](https://netreg.se/))**, funded by the Swedish Research Council (grant 2024-01861).

# References

Csardi, Gabor, and Tamas Nepusz. 2006. “The igraph Software Package for Complex Network Research.” *InterJournal, Complex Systems* 1695.

Hagberg, Aric A., Daniel A. Schult, and Pieter J. Swart. 2008. “Exploring Network Structure, Dynamics, and Function Using NetworkX.” In *Proceedings of the 7th Python in Science Conference*, 11–15.
