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
.start_threadle <- function(path = "Threadle.CLIconsole.exe") {
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
.stop_threadle <- function() {
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
set_workdir <- function(dir) {
  .send_command(sprintf("setwd(dir=\"%s\")", dir))
}

#' Get the current working directory from Threadle CLI
#'
#' @return The working directory as returned by Threadle.
#' @export
get_workdir <- function() {
  .send_command(sprintf("getwd()"))
  #out <- .send_command(sprintf("getwd()"))
  #out[1]
}

#' View an object inside the Threadle CLI environment
#'
#' @param name Name of the object to view.
#' @return CLI output.
#' @export
view <- function(name) {
  .send_command(sprintf("view(%s)", name))
}

#' Create a new nodeset in Threadle
#'
#' @param name Name of the R variable to assign in the CLI environment.
#' @return A `threadle_nodeset` object.
#' @export
create_nodeset <- function(name) {
  .send_command(sprintf("%s = createnodeset()", name))
  structure(list(name=name), class="threadle_nodeset")
}

#' Create a new network in Threadle
#'
#' @param name Name of the network variable.
#' @param nodeset A `threadle_nodeset` object.
#' @param label Optional display label.
#'
#' @return A `threadle_network` object.
#' @export
create_network <- function(name, nodeset, label = NULL) {
  label_arg <- if (!is.null(label)) sprintf(",name=%s", label) else ""
  .send_command(sprintf("%s = createnetwork(nodeset=%s%s)", name, nodeset$name, label_arg))
  structure(list(name = name), class = "threadle_network")
}

#' Load a file into Threadle
#'
#' @param name Name for the resulting object.
#' @param file File path.
#' @param type Type of structure (e.g. "network", "nodeset").
#'
#' @return An object with class corresponding to the loaded type.
#' @export
load_file <- function(name, file, type) {
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
load_network <- function(name, file) {
  .send_command(sprintf("%s = loadfile(file=\"%s\", type=network)",name, file))
  structure(list(name=name, nodeset=paste0(name,"_nodeset"), class="threadle_network"))
  #structure(list(name=paste0(name,"_nodeset"), class="threadle_nodeset"))
}

#' Retrieve information from a Threadle object
#'
#' @param name Name of the structure.
#' @param format Output format ("json" or raw text).
#'
#' @return Parsed JSON (list) or raw CLI text.
#' @export
info <- function(name, format="json") {
  retval <- .send_command(sprintf("info(name=%s, format=%s)",name, format))
  if (format =="json")
    fromJSON(retval)
  else
    retval
}

#' Add a node to a nodeset
#'
#' @param nodeset A `threadle_nodeset` object.
#' @param id Node ID.
#'
#' @return CLI output.
#' @export
add_node <- function(nodeset, id) {
  .send_command(sprintf("addnode(nodeset=%s, id=%d)", nodeset$name, id))
  #invisible(nodeset)
}

#' List all objects currently defined in Threadle
#'
#' @param format Output format ("json" or raw text).
#'
#' @return Parsed JSON or raw text.
#' @export
inventory <- function(format = "json") {
  retval <- .send_command(sprintf("i(format=json)"))
  if (format == "json")
    fromJSON(retval)
  else
    retval
}

#' Define an attribute for a nodeset
#'
#' @param nodeset A `threadle_nodeset` object.
#' @param attrname Name of the attribute.
#' @param attrtype Attribute type.
#'
#' @return CLI output.
#' @export
define_attr <- function(nodeset, attrname, attrtype) {
  cli <- sprintf("defineattr(nodeset=%s, attrname=%s, attrtype=%s)", nodeset$name, attrname, attrtype)
  .send_command(cli)
}

#' Set the value of a node attribute
#'
#' @param nodeset A `threadle_nodeset` object.
#' @param nodeid Node ID.
#' @param attrname Attribute name.
#' @param attrvalue Value to assign.
#'
#' @return CLI output.
#' @export
set_attr <- function(nodeset, nodeid, attrname, attrvalue) {
  cli <- sprintf("setattr(nodeset=%s,nodeid=%d,attrname=%s,attrvalue=%s)",nodeset$name,nodeid,attrname, attrvalue)
  .send_command(cli)
}

#' Get the value of a node attribute
#'
#' @param nodeset Nodeset name or object.
#' @param nodeid Node ID.
#' @param attrname Attribute name.
#'
#' @return CLI output.
#' @export
get_attr <- function(nodeset, nodeid, attrname) {
  #getattr(nodeset=[var:nodeset],nodeid=[nodeid],attrname=[attributeName])
  cli <- sprintf("getattr(structure=%s,nodeid=%d,attrname=%s)",nodeset,nodeid,attrname)
  #print(cli)
  .send_command(cli)
}

#' Define a layer in a network
#'
#' @param network A `threadle_network` object.
#' @param layername Name of the layer.
#' @param mode Layer mode (1 or 2).
#' @param directed Logical; whether ties are directed.
#' @param valuetype "binary" or "valued".
#' @param selfties Logical; whether self-ties are allowed.
#'
#' @return CLI output.
#' @export
define_layer <- function(network, layername, mode, directed=FALSE, valuetype="binary", selfties=FALSE) {
  # definelayer(network=[var:network], layername=[layerName], mode=['1','2'], *directed=['true','false'], *valuetype=['binary','valued'], *selfties=['true','false'])
  cli <- sprintf("definelayer(network=%s, layername=%s, mode=%d, directed=%s, valuetype=%s, selfties=%s)", network$name, layername, mode, directed,valuetype,selfties)
  .send_command(cli)
}

#' Get the number of nodes in a structure
#'
#' @param name Name of the structure.
#'
#' @return A numeric value.
#' @export
get_nbr_nodes <- function(name) {
  cli <- sprintf("getnbrnodes(structure=%s)",name)
  as.numeric(.send_command(cli))
}

#' Get a node ID by index
#'
#' @param name Name of the structure.
#' @param index Numeric index.
#'
#' @return The node ID.
#' @export
get_nodeid_by_index <- function(name, index) {
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
get_node_alters <- function(name,layername,nodeid,direction="both") {
  cli <- sprintf("getnodealters(network=%s, layername=%s,nodeid=%d, direction=%s)",name, layername, nodeid, direction)
  fromJSON(.send_command(cli))
}

#' Get a random alter for a node
#'
#' @param name Network name.
#' @param nodeid Node ID.
#' @param layername Optional layer.
#' @param direction Direction ("both", "in", "out").
#' @param balanced Whether selection should be balanced.
#'
#' @return A node ID (numeric).
#' @export
get_random_alter <- function(name, nodeid, layername="", direction="both", balanced="false") {
  cli <- sprintf("getrandomalter(network=%s, nodeid=%d, layername=%s, direction=%s, balanced=%s)",name, nodeid, layername,direction,balanced)
  as.numeric(.send_command(cli))
}

#' Get a random node from a structure
#'
#' @param name Structure name.
#' @return A node ID (numeric).
#' @export
get_random_node <- function(name) {
  cli <- sprintf("getrandomnode(structure=%s)",name)
  as.numeric(.send_command(cli))
}
