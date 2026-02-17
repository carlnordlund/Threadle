# Threadle: Test scripts
## Introduction to testing Threadle

The repository contains two CLIconsole scripts to test some basic features in Threadle
Note that these scripts are Threadle scripts, meaning that they are text files containing
a sequence of Threadle-specific CLI commands that can be entered into the Socnet.se CLI console.

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
**Script**: create.txt

To run the script, type in:
```bash
loadscript(Scripts/create.txt)
```

This test will create a nodeset with nodes and some attributes, and a network using that nodeset, subsequently creating three layers of relations with different properties
and populating these with edges. It will finally calcuate the density of one of these layers.

The second half of the script (line 81 oonwards) is commented out: this contains manual tests that can be done to verify that the network was created as it should have been.
Once the script has run, it is recommended to open the script and execute these command manually, to verify that the output is as given in the script file.

## Test 2: to-do

