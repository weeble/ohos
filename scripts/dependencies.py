#!/bin/env python

# Used by build.py
# Read dependency info file (normally dependencies.txt) and fetch them

import os
import shlex
import tarfile
import string

def default_log(logfile=None):
    return logfile if logfile is not None else open(os.devnull, "w")

class Dependency(object):
    def __init__(self, name, remotepath, localpath, configureargs, logfile=None):
        """
        name: A name to identify the dependency in build.py
        remotepath: The path of the dependency archive
        localpath: The path (relative to source root) to extract the archive
        configureargs: A list of arguments to append to the call to waf configure
        """
        self.name = name
        self.remotepath = remotepath
        self.localpath = localpath
        self.configureargs = configureargs
        self.logfile = default_log(logfile)
    def expand_remote_path(self, env):
        return string.Template(self.remotepath).substitute(env)
    def expand_local_path(self, env):
        return string.Template(self.localpath).substitute(env)
    def expand_configure_args(self, env):
        return [string.Template(arg).substitute(env) for arg in self.configureargs]
    def fetch(self, env):
        remote_path = self.expand_remote_path(env)
        local_path = os.path.abspath(self.expand_local_path(env))
        self.logfile.write("Fetching '%s' from '%s' and unpacking to '%s'... " % (self.name, remote_path, local_path))
        tar = tarfile.open(remote_path)
        try:
            os.makedirs(local_path)
        except OSError:
            # We get an error if the directory exists, which we are happy to
            # ignore. If something worse went wrong, we will find out very
            # soon when we try to extract the files.
            pass
        tar.extractall(local_path)
        tar.close()
        self.logfile.write("Done.\n")

def read_dependencies(dependencyfile, logfile):
    dependencies = {}
    for index, line in enumerate(dependencyfile):
        lineelements = shlex.split(line, comments=True)
        if len(lineelements)==0:
            continue
        if len(lineelements)!=4:
            raise Exception("Bad format in dependencies file, line %s." % (index + 1))
        dependencies[lineelements[0]] = Dependency(
                name=lineelements[0],
                remotepath=lineelements[1],
                localpath=lineelements[2],
                configureargs=shlex.split(lineelements[3]),
                logfile=logfile)
    return dependencies

def read_dependencies_from_filename(filename, logfile):
    dependencyfile = open(filename, "r")
    try:
        return read_dependencies(dependencyfile, logfile)
    finally:
        dependencyfile.close()

def fetch_dependencies(dependency_filename, dependency_names, env, logfile=None):
    """
    Fetch the specified dependencies.
    Return their concatenated configure arguments.
    """
    logfile = default_log(logfile)
    logfile.write("Required dependencies: " + ' '.join(dependency_names) + '\n')
    dependencies = read_dependencies_from_filename(dependency_filename, logfile)
    missing_dependencies = [name for name in dependency_names if name not in dependencies]
    if len(missing_dependencies) > 0:
        raise Exception("No entries in dependency file named: " + ", ".join(missing_dependencies) + ".")
    configure_args = []
    for name in dependency_names:
        dependencies[name].fetch(env)
        configure_args.extend(dependencies[name].expand_configure_args(env))
    return configure_args
