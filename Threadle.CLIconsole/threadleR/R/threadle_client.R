# R client package for Threadle CLIconsole

library(processx)
library(jsonlite)

#' Start a Threadle CLI console process
#'
#' Launches the Threadle.CLIconsole executable and stores the process handle
#' in the global environment as `.threadle_proc`. The process is started with
#' silent mode and an end marker to delimit responses.
#'
#' @param path Path to the Threadle CLI executable.
#' @return Invisibly returns the `processx` process object.
#' @export
th_start_threadle <- function(path = "Threadle.CLIconsole.exe") {
  if (exists(".threadle_proc", envir=.GlobalEnv)) {
    stop("'.threadle_proc' process already running.")
  }
  proc <- process$new(path, args=c("--silent","--endmarker"), stdin="|", stdout="|", stderr = "|")
  #proc <- process$new(path, args=c("--endmarker"), stdin="|", stdout="|", stderr = "|")
  assign(".threadle_proc", proc, envir=.GlobalEnv)
  invisible(proc)
}

#' Stop the running Threadle CLI console process
#'
#' Terminates the Threadle process previously started with `.start_threadle()`.
#'
#' @return None; prints status messages.
#' @export
th_stop_threadle <- function() {
  if (exists(".threadle_proc", envir=.GlobalEnv)) {
    proc <- get(".threadle_proc", envir=.GlobalEnv)
    
    if (proc$is_alive()) {
      proc$kill()
      message("'.threadle_proc' process terminated.")
    }
    else {
      message("'.threadle_proc' process is already not running.")
    }
    rm(".threadle_proc", envir = .GlobalEnv)
  }
  else {
    message("No '.threadle_proc' process found.")
  }
}

#' Send a command to the Threadle CLI backend
#'
#' Internal helper function used by all wrappers. Sends a command string and
#' waits for the `__END__` marker, returning all intermediate output lines.
#'
#' @param cmd A character string containing the CLI command to send.
#' @return A character vector with output lines.
#' @keywords internal
.send_command <- function(cmd) {
  #print(cmd)
  proc <- get(".threadle_proc", envir=.GlobalEnv)
  proc$write_input(paste0(cmd,"\n"))
  #print("ok, sent")
  out <- character()
  repeat {
    new <- proc$read_output_lines()
    #print(new)
    if (length(new) > 0) {
      out <- c(out, new)
      #out
      if (any(new == "__END__")) {
        out <- out[out != "__END__"]  # drop the marker
        break
      }
    } else {
      Sys.sleep(0.01)  # small pause between polls
    }
  }
  if (length(out)>0)
    out
}



#' Set working directory inside the Threadle CLI environment
#'
#' @param dir Path to the directory.
#' @return CLI output as a character vector.
#' @examples
#' set_workdir("~/data")
#' @export
th_set_workdir <- function(dir) {
  .send_command(sprintf("setwd(dir=\"%s\")", dir))
}


#' Get the current working directory from Threadle CLI
#'
#' @return The working directory as returned by Threadle.
#' @export
th_get_workdir <- function() {
  .send_command(sprintf("getwd()"))
  #out <- .send_command(sprintf("getwd()"))
  #out[1]
}

#' View a structure by variable in the Threadle CLI environment
#'
#' @param name Name of the object to view.
#' @return CLI output.
#' @export
th_view <- function(name) {
  .send_command(sprintf("view(structure=%s)", name))
}

#' Create a new nodeset in Threadle and assign it to variable 'name' in the Threadle CLI environment
#'
#' @param name Name of the R variable to assign in the CLI environment.
#' @return A `threadle_nodeset` object.
#' @export
th_create_nodeset <- function(name) {
  .send_command(sprintf("%s = createnodeset()", name))
  structure(list(name=name), class="threadle_nodeset")
}

#' Create a new network in Threadle and assign it to variable 'name' in the Threadle CLI environment
#' Create a new network in Threadle
#'
#' @param name Name of the assigned variable in the Threadle CLI environment.
#' @param nodeset A `threadle_nodeset` object.
#' @param label Optional internal name of network in Threadle.
#'
#' @return A `threadle_network` object.
#' @export
th_create_network <- function(name, nodeset, label = NULL) {
  label_arg <- if (!is.null(label)) sprintf(",name=%s", label) else ""
  .send_command(sprintf("%s = createnetwork(nodeset=%s%s)", name, nodeset$name, label_arg))
  structure(list(name = name), class = "threadle_network")
}

#' Load a file into Threadle and assigns the structure(s) to the provided variable name in the Threadle CLI environment
#'
#' @param name Name of the assigned variable in the Threadle CLI environment.
#' @param file File path.
#' @param type Type of structure ("network" or "nodeset").
#'
#' @return An object with class corresponding to the loaded type.
#' @export
th_load_file <- function(name, file, type) {
  .send_command(sprintf("%s = loadfile(file=\"%s\", type=%s)",name, file, type))
  structure(list(name=name), class=paste0("threadle_",type))
}

#' Load a network structure from a file
#'
#' @param name Name for the network object.
#' @param file Path to the network file.
#'
#' @return A `threadle_network` object.
#' @export
th_load_network <- function(name, file) {
  .send_command(sprintf("%s = loadfile(file=\"%s\", type=network)",name, file))
  structure(list(name=name, nodeset=paste0(name,"_nodeset"), class="threadle_network"))
  #structure(list(name=paste0(name,"_nodeset"), class="threadle_nodeset"))
}

#' Retrieve meta information from a Threadle object
#'
#' @param structure A `threadle_nodeset` or `threadle_network` object.
#' @param format Output format ("json"(default) or "console").
#'
#' @return Parsed JSON or raw CLI text.
#' @export
th_info <- function(structure, format="json") {
  retval <- .send_command(sprintf("info(structure=%s, format=%s)",structure$name, format))
  if (format =="json")
    fromJSON(retval)
  else
    retval
}

#' Add a node to a nodeset (or network)
#'
#' @param structure A `threadle_nodeset` or `threadle_network` object.
#' @param id Node ID.
#'
#' @return CLI output.
#' @export
th_add_node <- function(structure, id) {
  .send_command(sprintf("addnode(structure=%s, id=%d)", structure$name, id))
}

#' List all objects currently stored as variables in Threadle
#'
#' @param format Output format ("json"(default) or console").
#'
#' @return Parsed JSON or raw text.
#' @export
th_inventory <- function(format = "json") {
  retval <- .send_command(sprintf("i(format=json)"))
  if (format == "json")
    fromJSON(retval)
  else
    retval
}

#' Define an attribute for a nodeset (or network)
#'
#' @param structure A `threadle_nodeset` or `threadle_network` object.
#' @param attrname Name of the attribute.
#' @param attrtype Attribute type ("int","float", "char" or "bool")
#'
#' @return CLI output.
#' @export
th_define_attr <- function(structure, attrname, attrtype) {
  cli <- sprintf("defineattr(structure=%s, attrname=%s, attrtype=%s)", structure$name, attrname, attrtype)
  .send_command(cli)
}

#' Set the value of a node attribute for a nodeset (or network)
#'
#' @param structure A `threadle_nodeset` or `threadle_network` object.
#' @param nodeid Node ID.
#' @param attrname Attribute name.
#' @param attrvalue Value to assign.
#'
#' @return CLI output.
#' @export
th_set_attr <- function(structure, nodeid, attrname, attrvalue) {
  cli <- sprintf("setattr(structure=%s,nodeid=%d,attrname=%s,attrvalue=%s)",structure$name,nodeid,attrname, attrvalue)
  .send_command(cli)
}

#' Get the value of a node attribute for a nodeset (or network)
#'
#' @param structure A `threadle_nodeset` or `threadle_network` object.
#' @param nodeid Node ID.
#' @param attrname Attribute name.
#'
#' @return CLI output.
#' @export
th_get_attr <- function(structure, nodeid, attrname) {
  cli <- sprintf("getattr(structure=%s,nodeid=%d,attrname=%s)",structure$name,nodeid,attrname)
  .send_command(cli)
}

#' Adds/defines a relational layer in a network
#'
#' @param network A `threadle_network` object.
#' @param layername Name of the layer.
#' @param mode Layer mode (1 or 2).
#' @param directed Logical; whether ties are directed (only for 1-mode layers).
#' @param valuetype "binary" or "valued" (only for 1-mode layers).
#' @param selfties Logical; whether self-ties are allowed (only for 1-mode layers).
#'
#' @return CLI output.
#' @export
th_add_layer <- function(network, layername, mode, directed=FALSE, valuetype="binary", selfties=FALSE) {
  cli <- sprintf("addlayer(network=%s, layername=%s, mode=%d, directed=%s, valuetype=%s, selfties=%s)", network$name, layername, mode, directed,valuetype,selfties)
  .send_command(cli)
}

#' Get the number of nodes in a structure
#'
#' @param name Name of the structure (can be a network or nodeset).
#'
#' @return A numeric value.
#' @export
th_get_nbr_nodes <- function(name) {
  cli <- sprintf("getnbrnodes(structure=%s)",name)
  as.numeric(.send_command(cli))
}

#' Get a node ID by index
#'
#' @param name Name of the structure (can be network or nodeset).
#' @param index Numeric index.
#'
#' @return The node ID.
#' @export
th_get_nodeid_by_index <- function(name, index) {
  cli <- sprintf("getnodeidbyindex(structure=%s, index=%d)",name, index)
  as.numeric(.send_command(cli))
}

#' Get alters of a node within a network layer
#'
#' @param name Name of the network.
#' @param layername Layer to query.
#' @param nodeid Node ID.
#' @param direction Tie direction ("both", "in", "out").
#'
#' @return Parsed JSON list of alters.
#' @export
th_get_node_alters <- function(name,layername,nodeid,direction="both") {
  cli <- sprintf("getnodealters(network=%s, layername=%s,nodeid=%d, direction=%s)",name, layername, nodeid, direction)
  fromJSON(.send_command(cli))
}

#' Get a random alter for a node
#'
#' @param name Network name.
#' @param nodeid Node ID.
#' @param layername Optional layer. If left blank, will pick from all layers
#' @param direction Direction ("both"(default), "in", "out").
#' @param balanced Whether selection should be balanced for multiple layers.
#'
#' @return A node ID (numeric).
#' @export
th_get_random_alter <- function(name, nodeid, layername="", direction="both", balanced="false") {
  cli <- sprintf("getrandomalter(network=%s, nodeid=%d, layername=%s, direction=%s, balanced=%s)",name, nodeid, layername,direction,balanced)
  as.numeric(.send_command(cli))
}

#' Get a random node from a structure
#'
#' @param name Structure name.
#' @return A node ID (numeric).
#' @export
th_get_random_node <- function(name) {
  cli <- sprintf("getrandomnode(structure=%s)",name)
  as.numeric(.send_command(cli))
}
