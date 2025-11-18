# R client package for Threadle CLIconsole

library(processx)
library(jsonlite)

.start_threadle <- function(path = "Threadle.CLIconsole.exe") {
  if (exists(".threadle_proc", envir=.GlobalEnv)) {
    stop("'.threadle_proc' process already running.")
  }
  proc <- process$new(path, args=c("--silent","--endmarker"), stdin="|", stdout="|", stderr = "|")
  #proc <- process$new(path, args=c("--endmarker"), stdin="|", stdout="|", stderr = "|")
  assign(".threadle_proc", proc, envir=.GlobalEnv)
  invisible(proc)
}

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

set_workdir <- function(dir) {
  .send_command(sprintf("setwd(dir=\"%s\")", dir))
}

get_workdir <- function() {
  .send_command(sprintf("getwd()"))
  #out <- .send_command(sprintf("getwd()"))
  #out[1]
}

view <- function(name) {
  .send_command(sprintf("view(%s)", name))
}

# the provided 'name' is what the internal variable 
create_nodeset <- function(name) {
  .send_command(sprintf("%s = createnodeset()", name))
  structure(list(name=name), class="threadle_nodeset")
}

create_network <- function(name, nodeset, label = NULL) {
  label_arg <- if (!is.null(label)) sprintf(",name=%s", label) else ""
  .send_command(sprintf("%s = createnetwork(nodeset=%s%s)", name, nodeset$name, label_arg))
  structure(list(name = name), class = "threadle_network")
}

load_file <- function(name, file, type) {
  .send_command(sprintf("%s = loadfile(file=\"%s\", type=%s)",name, file, type))
  structure(list(name=name), class=paste0("threadle_",type))
}

load_network <- function(name, file) {
  .send_command(sprintf("%s = loadfile(file=\"%s\", type=network)",name, file))
  structure(list(name=name, nodeset=paste0(name,"_nodeset"), class="threadle_network"))
  #structure(list(name=paste0(name,"_nodeset"), class="threadle_nodeset"))
}

info <- function(name, format="json") {
  retval <- .send_command(sprintf("info(name=%s, format=%s)",name, format))
  if (format =="json")
    fromJSON(retval)
  else
    retval
}

add_node <- function(nodeset, id) {
  .send_command(sprintf("addnode(nodeset=%s, id=%d)", nodeset$name, id))
  #invisible(nodeset)
}

inventory <- function() {
  .send_command(sprintf("i()"))
}

define_attr <- function(nodeset, attrname, attrtype) {
  cli <- sprintf("defineattr(nodeset=%s, attrname=%s, attrtype=%s)", nodeset$name, attrname, attrtype)
  .send_command(cli)
}

set_attr <- function(nodeset, nodeid, attrname, attrvalue) {
  cli <- sprintf("setattr(nodeset=%s,nodeid=%d,attrname=%s,attrvalue=%s)",nodeset$name,nodeid,attrname, attrvalue)
  .send_command(cli)
}

get_attr <- function(nodeset, nodeid, attrname) {
  #getattr(nodeset=[var:nodeset],nodeid=[nodeid],attrname=[attributeName])
  cli <- sprintf("getattr(nodeset=%s,nodeid=%d,attrname=%s)",nodeset,nodeid,attrname)
  .send_command(cli)
}

define_layer <- function(network, layername, mode, directed=FALSE, valuetype="binary", selfties=FALSE) {
  # definelayer(network=[var:network], layername=[layerName], mode=['1','2'], *directed=['true','false'], *valuetype=['binary','valued'], *selfties=['true','false'])
  cli <- sprintf("definelayer(network=%s, layername=%s, mode=%d, directed=%s, valuetype=%s, selfties=%s)", network$name, layername, mode, directed,valuetype,selfties)
  .send_command(cli)
}

get_nbr_nodes <- function(name) {
  cli <- sprintf("getnbrnodes(structure=%s)",name)
  as.numeric(.send_command(cli))
}

get_nodeid_by_index <- function(name, index) {
  cli <- sprintf("getnodeidbyindex(structure=%s, index=%d)",name, index)
  as.numeric(.send_command(cli))
}

get_node_alters <- function(name,layername,nodeid,direction="both") {
  cli <- sprintf("getnodealters(network=%s, layername=%s,nodeid=%d, direction=%s)",name, layername, nodeid, direction)
  fromJSON(.send_command(cli))
}

get_random_alter <- function(name, nodeid, layername="", direction="both", balanced="false") {
  cli <- sprintf("getrandomalter(network=%s, nodeid=%d, layername=%s, direction=%s, balanced=%s)",name, nodeid, layername,direction,balanced)
  as.numeric(.send_command(cli))
}

get_random_node <- function(name) {
  cli <- sprintf("getrandomnode(structure=%s)",name)
  as.numeric(.send_command(cli))
}
