#!/bin/env python

# This script is specific to Linn's network environment. It copies the
# project's dependencies from a known location on the network into the
# dependencies folder. After running this, you should be able to run
# "waf configure" and it should detect all the dependencies.

# DO NOT USE THIS SCRIPT if you do not have access to Linn's network.

# If you are building this project without access to the Linn network,
# you are responsible for building or downloading the dependencies and
# either placing them in the dependencies folder or specifying their
# locations elsewhere when you invoke "waf configure".

import subprocess
import sys
import os
import platform
import shutil
from dependencies import (
        fetch_dependencies)
import threading
import optparse

def get_vsvars_environment():
    """
    Returns a dictionary containing the environment variables set up by vsvars32.bat

    win32-specific
    """
    vs100comntools = os.environ['VS100COMNTOOLS']
    if vs100comntools is None:
        raise Exception("VS100COMNTOOLS is not set in environment.")
    vsvars32 = os.path.join(vs100comntools, 'vsvars32.bat')
    python = sys.executable
    process = subprocess.Popen('("%s">nul)&&"%s" -c "import os; print repr(os.environ)"' % (vsvars32, python), stdout=subprocess.PIPE, shell=True)
    stdout, _ = process.communicate()
    exitcode = process.wait()
    if exitcode != 0:
        raise Exception("Got error code %s from subprocess!" % exitcode)
    return eval(stdout.strip())

def default_platform():
    if platform.system() == 'Windows':
        return 'Windows-x86'
    if platform.system() == 'Linux' and platform.architecture()[0] == '32bit':
        return 'Linux-x86'
    return None

def delete_directory(path, logfile=None):
    if logfile is None:
        logfile = open(os.devnull, "w")
    path = os.path.abspath(path)
    logfile.write('Deleting "'+path+'"... ')
    shutil.rmtree(path, ignore_errors=True)
    if os.path.isdir(path):
        logfile.write('\nFailed.\n')
        raise Exception('Failed to delete "%s"' % path)
    logfile.write('\nDone.\n')

# Mapping from Hudson/Jenkins slave group labels to platform names.
platforms_for_slaves = {
        "windows-x86" : "Windows-x86",
        "linux-x86" : "Linux-x86",
        "linux-x64" : "Linux-x64",
        "arm" : "Linux-ARM",
        None : default_platform()
        }

def parse_command_line(argv=None):
    if argv is None:
        argv = sys.argv
    parser = optparse.OptionParser()
    parser.add_option(
            "-p", "--publish-revno",
            dest="publishrevno",
            action="store_true",
            default=False,
            help="Publish the current revision number to a network share.")
    parser.add_option(
            "-t", "--target",
            dest="target",
            default=None,
            help="Target platform. One of {%s}." % (", ".join(build_behaviours.keys())))
    parser.add_option(
            "-a", "--artifacts",
            dest="artifacts",
            default=None,
            help="Build artifacts directory. Used to fetch dependencies. Default depends on target platform.")
    return parser.parse_args(argv)

def windows_prebuild(env):
    # Connect to the network share.
    sys.stdout.write("Connecting to network share...\n")
    sys.stdout.flush()
    subprocess.check_call(["net", "use", "\\\\ohnet.linn.co.uk\\artifacts"])
    # Use vsvars32.bat to add Visual Studio settings to environment.
    env.update(get_vsvars_environment())

class BuildBehaviour(object):
    def __init__(
            self,
            prebuild = None,
            wipe_dependencies = True,
            dependencies_to_copy = (),
            run_configure = True,
            custom_test_func = None,
            extra_configure_args = (),
            env = {}):
        self.prebuild = prebuild if prebuild is not None else lambda env: None
        self.wipe_dependencies = wipe_dependencies
        self.dependencies_to_copy = dependencies_to_copy
        self.should_run_configure = run_configure
        self.custom_test_func = custom_test_func
        self.extra_configure_args = extra_configure_args
        self.env = env

# Behaviour for the build when building for each platform.
build_behaviours = {
        "Windows-x86" : BuildBehaviour(
            prebuild = windows_prebuild,
            dependencies_to_copy = ["ohnet", "nunit", "ndesk-options", "yui-compressor", "mono-addins"],
            run_configure = True,
            env = dict(
                PLATFORM='Windows-x86',
                OHNET_ARTIFACTS='\\\\ohnet.linn.co.uk\\artifacts',
                WAFLOCK='.lock-wafbuildwindows',
                ZAPP_NO_ERROR_DIALOGS='1')),

        "Windows-x64" : BuildBehaviour(
            prebuild = windows_prebuild,
            dependencies_to_copy = ["ohnet", "nunit", "ndesk-options", "yui-compressor", "mono-addins"],
            run_configure = True,
            env = dict(
                PLATFORM='Windows-x64',
                OHNET_ARTIFACTS='\\\\ohnet.linn.co.uk\\artifacts',
                WAFLOCK='.lock-wafbuildwindows64',
                ZAPP_NO_ERROR_DIALOGS='1')),

        "Linux-x86" : BuildBehaviour(
            dependencies_to_copy = ["ohnet", "nunit", "ndesk-options", "yui-compressor", "mono-addins"],
            run_configure = True,
            extra_configure_args = ["--with-csc-binary", "/usr/bin/gmcs"],
            env = dict(
                PLATFORM='Linux-x86',
                OHNET_ARTIFACTS='/opt/artifacts',
                WAFLOCK='.lock-wafbuildlinux')),

        "Linux-x64" : BuildBehaviour(
            dependencies_to_copy = ["ohnet", "nunit", "ndesk-options", "yui-compressor", "mono-addins"],
            run_configure = True,
            extra_configure_args = ["--with-csc-binary", "/usr/bin/gmcs"],
            env = dict(
                PLATFORM='Linux-x64',
                OHNET_ARTIFACTS='/opt/artifacts',
                WAFLOCK='.lock-wafbuildlinux64')),

        "Linux-ARM" : BuildBehaviour(
            dependencies_to_copy = ["ohnet", "nunit", "ndesk-options", "yui-compressor", "mono-addins"],
            run_configure = True,
            extra_configure_args = ["--with-csc-binary", "/usr/bin/gmcs", "--platform", "Linux-ARM"],
            #custom_test_func = run_tests_on_sheeva,
            env = dict(
                PLATFORM='Linux-ARM',
                OHNET_ARTIFACTS='/opt/artifacts',
                WAFLOCK='.lock-wafbuildarm')),
        }

class Build(object):
    def __init__(self):
        self.options, self.args = parse_command_line()

        if self.options.target is not None:
            platform = self.platform = self.options.target
        else:
            platform = self.platform = get_default_platform()
        behaviour = self.behaviour = build_behaviours[platform]

        env = self.env = dict(os.environ)
        env.update(behaviour.env)

        if self.options.artifacts is not None:
            env['OHNET_ARTIFACTS'] = self.options.artifacts

        configure_args = self.configure_args = []
        configure_args.extend(behaviour.extra_configure_args)

    def prebuild(self):
        self.behaviour.prebuild(self.env)

    def writeenv(self):
        artifacts = self.env['OHNET_ARTIFACTS']
        artifacts = os.path.join(artifacts, 'ohos-revision.txt')
        f = open(artifacts, 'w')
        output = subprocess.check_call('git rev-parse HEAD', stdout=f, shell=True)
        f.close()
    
    def copy_dependencies(self):
        # Wipe the dependencies directory:
        if self.behaviour.wipe_dependencies:
            delete_directory(os.path.join('dependencies', self.platform), logfile=sys.stdout)

        # Fetch our dependencies according to 'dependencies.txt'.
        if len(self.behaviour.dependencies_to_copy) > 0:
            return self.configure_args + fetch_dependencies("scripts/dependencies.txt", self.behaviour.dependencies_to_copy, self.env, logfile=sys.stdout)
        else:
            return self.configure_args

    def configure(self, configure_args):
        if self.behaviour.should_run_configure:
            sys.stdout.write('\nConfigure...\n')
            sys.stdout.flush()
            subprocess.check_call([sys.executable, "waf", "configure"] + configure_args, env=self.env)

    def build(self):
        sys.stdout.write('\nBuild...\n')
        sys.stdout.flush()
        subprocess.check_call([sys.executable, "waf", "clean", "build"], env=self.env)

    def test(self):
        sys.stdout.write('\nTest...\n')
        sys.stdout.flush()
        if self.behaviour.custom_test_func is not None:
            self.behaviour.custom_test_func(self.env)
        else:
            subprocess.check_call([sys.executable, "waf", "test"], env=self.env)
            subprocess.check_call([sys.executable, "waf", "integrationtest"], env=self.env)
    
    def run(self):
        self.prebuild()
        configure_args = self.copy_dependencies()
        self.configure(configure_args)
        if self.options.publishrevno:
            self.writeenv()
        self.build()
        self.test()

def get_default_platform():
    slave = os.environ.get("slave", None) 
    return platforms_for_slaves[slave]

if __name__ == "__main__":
    builder = Build()
    builder.run()
