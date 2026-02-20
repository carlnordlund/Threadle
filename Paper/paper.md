---
title: "Threadle: A Memory-Efficient Network Storage and Query Engine for Large, Multilayer, and Mixed-mode Networks"
tags:
- networks
- C\#/.NET
- multilevel networks
- multimode networks
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
date: "18 February 2026"
bibliography: paper.bib
---

# Summary

**Threadle** ([www.threadle.dev](https://www.threadle.dev)) is an open-source, high-performance, and memory-efficient network storage and query engine written in C#. Designed specifically for working with full-population networks derived from Swedish administrative register data — representing very large, multilayer, mixed-mode, and heterogeneous node-attribute networks with millions of nodes and billions of edges — Threadle's design principles are also applicable to other large, feature-rich relational datasets requiring memory-efficient storage.

Unlike existing general-purpose network libraries that emphasize analytical algorithms, Threadle focuses explicitly on efficient storage, fast neighborhood retrieval, compact representation of multilayer and mixed-mode relationships, and scalable handling of node attributes. It provides a backend for implementing sample- and traversal-based analytical procedures at the frontend level.

Threadle natively supports multilayer structures, with relational data stored in respective layers according to their specific properties. Layers can be either 1-mode (unipartite) or 2-mode (bipartite), the latter storing collections of hyperedges and node affiliations. A core innovative feature is how 2-mode layers are queried as if they were 1-mode layers, without ever performing the memory-intensive projections required by established network management systems. Threadle also implements a memory-efficient system for storing node attributes of four data types (integer, floating-point, character, and boolean), optimized for heterogeneous distributions where certain attributes exist only for subsets of nodes.

The base functionality is implemented in the Threadle.Core module. A command-line console application (Threadle.CLIconsole) provides direct interaction using a Threadle-specific scripting language, and can operate in JSON mode for interoperability with other languages. We provide an R library frontend, **threadleR** ([GitHub](https://github.com/YukunJiao/threadleR)), enabling researchers to conduct advanced sampling- and traversal-based workflows on very large networks directly from R.

# Statement of need

Large-scale administrative register data, such as the Swedish population registers for research, can be represented and approached as large, temporal, and structurally complex networks containing multiple relational layers (kinship, residence, employment, education, etc.), many of which are two-mode (affiliation) structures. Research applications involving multilayer random-walker models, simulation-based methods, and repeated sampling of ego networks across layers require very fast retrieval of neighboring alters and node attributes, while maintaining a memory footprint small enough to allow population-scale networks for multiple years to be simultaneously loaded into RAM.

General-purpose libraries such as igraph [@igraph], NetworkX [@networkx], NetworKit [@staudt_networkit_2016], and graph-tool [@peixoto_graph-tool_2017] offer extensive analytical toolsets, but their internal data models are not designed to support the structural and memory requirements of multilayer and mixed-mode data found in full-population register data. Specifically, we note the following set of features of these existing libraries that are not optimal for working with more heterogeneous, feature-rich network data:

- Multilayer networks are typically represented by attaching attributes to edges rather than storing them natively within layers in the engine, with substantial memory overhead
- Relations are typically stored as unipartite graphs where all relations across layers and edge types must be represented as node-pairs in a single one-mode edgelist
- Two-mode (bipartite) data are commonly projected to their one-mode form, expanding each affiliation of $k$ nodes into $k(k−1)/2$ pairwise edges, which is memory-prohibitive for large bipartite datasets
- Node attributes are often stored in general-purpose metadata containers in R or Python rather than the graph engine itself
- Many commonly used methods and metrics—such as betweenness centrality, closeness centrality, and community detection—become computationally infeasible for very large networks due to their time and memory complexity, although approximation algorithms exist for some measures [cf. @staudt_networkit_2016].

Given these constraints, where analyses on population-scale networks necessarily rely on sample- and traversal-based methods, it is efficient storage and fast querying and data retrieval that are the critical requirements for working with such networks. Threadle was developed to address this need, providing a dedicated backend for representation and querying of full-population, feature-rich networks with multiple relational layers of different properties and modes. Threadle is thus not an alternative to igraph, NetworkX, NetworKit or similar network-analytical frameworks — but it is an alternative storage and query engine for large, feature-rich networks, where sample- and traversal-based analytical methods instead are implemented in the threadleR frontend.

# Software design and key features

Threadle's architecture comprises two principal components. *Threadle.Core* is a .NET 8.0 library implementing all data models, storage structures, processors, methods, and file I/O. Implementing a shared interface, the two fundamental structure types in Threadle are Nodesets and Networks.

A Nodeset is a lightweight collection of node identifiers, plus optional node attributes attached to nodes. Idenfitied by their unique unsigned integers, nodes are either stored in a hashset or a dictionary structure depending on whether they have node attributes, automatically switching between these two collections as they gain or lose attributes.

A Network, referencing a Nodeset, consists of layers of relations, where each node owns its edges or hyperedge memberships within each layer. Layers within a Network are defined as either 1-mode, each layer with its own configurable directionality, edge valuation, and self-tie allowance, or 2-mode, storing affiliations as hyperedges. One-mode layers store edges in the form of nodelists, i.e. where each node has references to its out- and inbound edges, but where the specific type of storage depends on whether the layer is binary or valued, and directional or symmetric. For layers with directional edges, both incoming and outgoing edges are stored by default, but in case only outbound edges are of interest, e.g. as with random walker algorithms, it is possible to disable the storage of inbound edges, thus reducing the necessary memory almost by a half.

Two-mode layers store a set of named hyperedges, each holding a collection of node ID's that are connected by the hyperedge, supplemented by a dictionary to quickly access the collections of hyperedges that a node is connected to. This allows for quickly finding the node alters of a node - as the union of its hyperedges' nodes - and to quickly check whether two nodes are connected - by traversing the collection of hyperedges of one of the nodes searching for the first occurrence of the other. The cardinality of the intersection of the hyperedges of two nodes results in the value of the symmetrical tie that would emerge from the classical type of 2-mode-to-1-mode projection.  As the classes for both one-mode and two-mode layers implement a shared interface with the same method signatures for querying edges, two-mode layers can efficiently be queried as if they were projected into their one-mode counterparts - such as the methods for checking edges, getting edges, and getting node alters as implemented in the LayerTwoMode class for two-mode layers:

```csharp
public bool CheckEdgeExists(uint node1Id, uint node2Id)
{
    // Early exit if either node has no affiliations/hyperedges
    if (GetNonEmptyHyperedgeCollection(node1Id) is not HyperedgeCollection col1
        || GetNonEmptyHyperedgeCollection(node2Id) is not HyperedgeCollection col2)
        return false;
    // O(min(n,m)) check using HashSet.Overlaps(), stops at first match
    return col1.HyperEdges.Overlaps(col2.HyperEdges);
}

public float GetEdgeValue(uint node1Id, uint node2Id)
{
    // Early exit if either node has no affiliations/hyperedges
    if (GetNonEmptyHyperedgeCollection(node1Id) is not HyperedgeCollection col1
      || GetNonEmptyHyperedgeCollection(node2Id) is not HyperedgeCollection col2)
        return 0f;

    // O(min(n,m)) - iterates smaller set, O(1) lookup in larger
    return col1.HyperEdges.Count < col2.HyperEdges.Count ?
      col1.HyperEdges.Count(h => col2.HyperEdges.Contains(h)) :
      col2.HyperEdges.Count(h => col1.HyperEdges.Contains(h));
}

public uint[] GetNodeAlters(uint nodeId, EdgeTraversal edgeTraversal)
{
    // Note: edgeTraversal: ILayer interface requirement for 1-mode layers
    // Exits with empty set if node has no affiliations/hyperedges
    if (GetNonEmptyHyperedgeCollection(nodeId) is not HyperedgeCollection col)
        return [];
    HashSet<uint> alters = [];
    // Iterates all node hyperedges, creates union of alters
    foreach (Hyperedge hyperEdge in col.HyperEdges)
        alters.UnionWith(hyperEdge.NodeIds);
    // Remove ego node
    alters.Remove(nodeId);
    return [.. alters];
}
```

*Threadle.CLIconsole* is a cross-platform command-line application exposing Core functionality through a scripting language. It operates in two modes: a human-readable text mode for interactive use, and a JSON mode enabling programmatic control from external systems. The *threadleR* package leverages JSON mode to provide seamless R integration, wrapping Threadle commands in R functions and enabling users to combine Threadle's efficient storage with R's statistical capabilities and conditional program flows.

Threadle supports both human-readable text file formats and a compact binary format, both optionally Gzip-compressed. Data can be imported from standard edgelist and matrix formats. Precompiled binaries are available for Windows, Linux, and macOS, and the software can be compiled from source. In addition to these binaries, the project website provides a thorough User Guide, covering installation instructions, a quick-start user guide, and more technical details and underlying software architecture, as well as a complete reference to all the 50+ CLI commands that Threadle understands. A separate section provides details on threadleR, alongside a vignette example using the provided Lazega lawyer network data [@lazega_collegial_2001].

# Research impact statement

Threadle was developed within *The Complete Network of Sweden* ([NetReg.se](https://netreg.se)) research environment, which aims to construct and analyze the comprehensive, full-population network of social exposure in Sweden using administrative register data. This network encompasses data on kinships, households, families, and affiliations (schools, workplaces, residential areas) for approximately 15 million individuals from 1990 onward, representing one of the most complete, multi-domain population-scale social network datasets available for research.

The fundamental research challenge that motivated Threadle's development is the impossibility of projecting large-scale 2-mode affiliation data using existing tools. For instance, representing workplace co-affiliation as projected 1-mode edges for Sweden's entire working population would require storing billions of edges, exhausting available RAM. Storing co-residency in a similar way would likely produce 1-mode edges of magnitudes more. Threadle's pseudo-projection approach — querying 2-mode data as if projected without materializing the projection — makes such analyses computationally tractable.

Using Threadle, we are also testing various random-walker methodologies for measuring network properties across multilayer population networks. These methods, implemented in threadleR, enable researchers to estimate network statistics through sampling and traversal rather than exhaustive computation, which is essential for networks of this scale.

Beyond the specific application that inspired its development, Threadle could also be useful in other scientific contexts and datasets, i.e. where very large multilayer networks consisting of both 1-mode (unipartite) and 2-mode (bipartite) data has to be queried as if the 2-mode data was projected.

# Use of AI statement

AI assistance (Claude Code) was occasionally used as an external discussant and stress-tester of architectural design ideas, as well as for knowledge-related queries (e.g., anatomy of InnoSetup files, buffer handling when decompressing Gzip streams, and parametrization of Watts-Strogatz random graph generation). Claude and the Microsoft Word readability tool were also used for proofreading and to improve readability of this article.

# Acknowledgements

Threadle was developed within the research environment The Complete Network of Sweden ([NetReg.se](https://netreg.se)), funded by the Swedish Research Council (grant 2024-01861).

# References
