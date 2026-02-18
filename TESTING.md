# Threadle: Test scripts
(For testing threadleR, please check the available scripts on its GitHub repository: [https://github.com/YukunJiao/threadleR/](https://github.com/YukunJiao/threadleR/))
## Introduction to testing Threadle

The repository contains two CLIconsole scripts to test some basic features in Threadle
Note that these scripts are Threadle scripts, meaning that they are text files containing
a sequence of Threadle-specific CLI commands that can be entered into the Threadle.CLIconsole client.

However, it is easier to use the CLI command `loadscript(..)` to load and execute a script file.

Before running the script file corresponding to each test, do the following:

- Start the Threadle client
- Make sure that the working directory is in the '/Examples' folder. Check where you are with
```bash
getwd()
```

If you are not in /Examples, set the current working directory of Threadle using the following CLI command:
```bash
setwd(dir = "[path_to_Examples_folder]")
```
Check that you are in the correct folder by using the `dir()` command:
```bash
dir
```
The `/Scripts/` folder should be visible in the list that appears in the client.


## Test 1: Create structures programmatically, query data
**Script**: test1_create.txt

To run the script, type in:
```bash
loadscript(Scripts/test1_create.txt)
```

This test will create a nodeset with nodes and some attributes, and a network using that nodeset, subsequently creating three layers of relations with different properties
and populating these with edges. It will finally calcuate the density of one of these layers.

The second half of the script (line 81 oonwards) is commented out: this contains manual tests that can be done to verify that the network was created as it should have been.
Once the script has run, it is recommended to open the script and execute these command manually, to verify that the output is as given in the script file.

## Test 2: Generate multilayer random networks, query and inbuilt analyses
**Script**: test2_generate.txt

***Do note that this script might take a while to run!***

To run the script, type in:
```bash
loadscript("Scripts/test2_generate.txt")
```

This test will create a nodeset and network with 1 million nodes. Four different relational layers containing random networks are then created:
- **ws**: Symmetric binary 1-mode layer for a Watts-Strogatz random network (k=50, beta=0.01)
- **ab**: Symmetric binary 1-mode layer for an Albert-Barabasi random network (m=20)
- **er**: Directional binary 1-mode layer for an Erdös-Renyi random network (p=0.00005)
- **twomode**: 2-mode layer for a random bipartite network with 50,000 hyperedges and where each node has on average 25 affiliations (Poisson-distribution)

The three 1-mode layers contain, respectively 25, 20, and 50 million edges. The 2-mode layer contains 25 million affiliations, corresponding to
approximately 6 billion 1-mode edges if these were to be projected.

The test will then continue obtaining alters/neighborhoods for a set of node ids, from both individual 1-mode and 2-mode layers as well as combinations thereof.

The shortest path between two arbitrarily selected nodes are also calculated for each layer, as well as all layers togethert.

The degree centrality for the 'ba' layer is then calculated for all nodes, and summary statistics for this attribute is calculated.

A new nodeset 'nodes2' is created that contains all the nodes in the original nodeset whose degree centrality in the 'ba' layer is greater than 40. This smaller nodeset
is then used to create a sub-network of the original network. That smaller network - 'net2' - is finally inspected.
