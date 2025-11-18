# testing popnet_client.R

# Set working directory in R - where the data is etc.
# setwd("C:/Users/pekpi/Nextcloud/Work/Academic/Individual projects/PopnetEngine software reasoning/R testing")

# Load the "library" with functions for communicating
source("Lib/threadle_client.R")

# path to the exe file
path_to_exe <-"../bin/Debug/net8.0/Threadle.CLIconsole.exe"



# Start a Threadle instance
.start_threadle(path_to_exe)

# Move into examples folder
set_workdir("../Examples")

# Load a network file into Threadle (will also load a nodeset file)
lazeganet <- load_network("lazega","lazega.tsv")

# Get an inventory of stored objects
inventory()

# Get info about the "lazega" structure
info_net <- info("lazega")
info_net

info_nodes <- info("lazega_nodeset")
info_nodes

# Get nbr of nodes in the network (can either use the network or nodeset)
nbr_nodes <- get_nbr_nodes("lazega")
nbr_nodes

# Get a random starting node
nodeid <- get_nodeid_by_index("lazega", sample(0:nbr_nodes-1,1))

# Get Office attribute
office_current <- get_attr("lazega_nodeset",nodeid,"Office")

# Get a random alter in the friends layer:
random_alter_nodeid <- get_random_alter("lazega", nodeid, layername = "friends")

# Get office attribute of this
office_alter <- get_attr("lazega_nodeset", random_alter_nodeid, "Office")

# etc - so this should be looped of course, but mechanism is all there for random walker now

#.stop_threadle()
