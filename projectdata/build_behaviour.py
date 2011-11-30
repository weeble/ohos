# Defines the build behaviour for continuous integration builds.
#
# Invoke with "go hudson_build"

# Maintenance notes:
#
# The following special functions are available for use in this file:
#
# add_option("-t", "--target", help="Set the target.")
#     Add a command-line option. See Python's optparse for arguments.
#     options are accessed on context.options. (See build_step.)
#
# fetch_dependencies("ohnet", "nunit", "zwave", platform="Linux-ARM")
# fetch_dependencies(["ohnet", "log4net"], platform="Windows-x86")
#     Fetches the specified dependencies for the specified platform. Omit platform
#     to use the platform defined in the PLATFORM environment variable.
#
# get_dependency_args("ohnet", "nunit", "zwave")
# get_dependency_args(["ohnet", "log4net"])
#     Returns a list of the arguments found in the dependencies.txt file
#     for the given dependencies, using the current environment.
#
# @build_step("name", optional=True, default=False)
# @build_condition(PLATFORM="Linux-x86")
# @build_condition(PLATFORM="Windoxs-x86")
# def your_build_step(context):
#     ...
#     Add a new build-step that only runs when one of the build conditions
#     matches. (Here if PLATFORM is either "Linux-x86" or "Windows-x86".)
#     Context will be an object with context.options and context.env defined.
#     Name argument is optional and defaults to the name of the function. If
#     optional is set to True you can enable or disable the step with
#     select_optional_steps, and default determines whether it will run by
#     default.
#
# select_optional_steps("+build", "-test")
# select_optional_steps("stresstest", disable_others=True)
#     Enables or disables optional steps. Use "+foo" to enable foo, and "-foo"
#     to disable it. Use 'disable_others=True' to disable all optional steps
#     other than those specifically enabled.
#
# python("waf", "build")
#     Invoke a Python subprocess. Provide arguments as strings or lists of
#     strings.
#
# rsync(...)
#     Invoke an rsync subprocess. See later for examples.
#
# shell(...)
#     Invoke a shell subprocess. Arguments similar to python().
#
# with SshSession(host, username) as ssh:
#     ssh("echo", "hello")
#
#     Connect via ssh and issue commands. Command arguments similar to python().
#  

import os
import shutil

require_version(5)


# Command-line options. See documentation for Python's optparse module.
add_option("-t", "--target", help="Target platform. One of Windows-x86, Windows-x64, Linux-x86, Linux-x64, Linux-ARM.")
add_option("-p", "--publish-version", help="Specify a version to publish.")
add_option("-a", "--artifacts", help="Build artifacts directory. Used to fetch dependencies.")
add_bool_option("-f", "--fetch-only", help="Fetch dependencies, skip building.")
add_bool_option("-F", "--no-fetch", help="Skip fetch dependencies.")
add_bool_option("--tests-only", help="Just run tests.")
add_bool_option("--no-tests", help="Don't run any tests.")
add_bool_option("--stresstest-only", help="Run stress tests.")
add_option("--steps", default="default", help="Steps to run, comma separated. (all,default,fetch,configure,build,tests,publish)")

ALL_DEPENDENCIES = [
    "ohnet",
    "nunit",
    "ndesk-options",
    "yui-compressor",
    "mono-addins",
    "log4net"]

@build_step()
def choose_optional_steps(context):
    specify_optional_steps(context.options.steps)
    if context.options.publish_version or context.env.get("PUBLISH_RELEASE","false").lower()=="true":
        modify_optional_steps("+publish")
    if context.options.fetch_only:
        specify_optional_steps("fetch")

# Unconditional build step. Choose a platform and set the
# appropriate environment variable.
@build_step()
def choose_platform(context):
    if context.options.target:
        context.env["PLATFORM"] = context.options.target
    elif "slave" in context.env:
        context.env["PLATFORM"] = {
                "windows-x86" : "Windows-x86",
                "windows-x64" : "Windows-x64",
                "linux-x86" : "Linux-x86",
                "linux-x64" : "Linux-x64",
                "arm" : "Linux-ARM",
            }[context.env["slave"]]
    else:
        context.env["PLATFORM"] = default_platform()

# Universal build configuration.
@build_step()
def setup_universal(context):
    env = context.env
    env.update(
        OHNET_ARTIFACTS=context.options.artifacts or 'http://www.openhome.org/releases/artifacts',
        OHOS_PUBLISH="releases@www.openhome.org:/home/releases/www/artifacts/ohOs",
        BUILDDIR='buildhudson',
        WAFLOCK='.lock-wafbuildhudson')
    context.configure_args = get_dependency_args(ALL_DEPENDENCIES)

# Extra Windows build configuration.
@build_step()
@build_condition(PLATFORM="Windows-x86")
@build_condition(PLATFORM="Windows-x64")
def setup_windows(context):
    env = context.env
    env.update(
        OPENHOME_NO_ERROR_DIALOGS="1",
        OHNET_NO_ERROR_DIALOGS="1")
    env.update(get_vsvars_environment())

# Extra Linux build configuration.
@build_step()
@build_condition(PLATFORM="Linux-x86")
@build_condition(PLATFORM="Linux-x64")
@build_condition(PLATFORM="Linux-ARM")
def setup_linux(context):
    env = context.env
    context.configure_args += ["--with-csc-binary", "/usr/bin/dmcs"]
    context.configure_args += ["--platform", env["PLATFORM"]]

# Principal build steps.
@build_step("fetch", optional=True)
def fetch(context):
    fetch_dependencies(ALL_DEPENDENCIES)

@build_step("configure", optional=True)
def configure(context):
    python("waf", "configure", context.configure_args)

@build_step("build", optional=True)
def build(context):
    python("waf")

@build_step("tests", optional=True)
@build_condition(PLATFORM="Windows-x86")
@build_condition(PLATFORM="Windows-x64")
@build_condition(PLATFORM="Linux-x86")
@build_condition(PLATFORM="Linux-x64")
def tests_normal(context):
    python("waf", "test")
    python("waf", "integrationtest")

@build_step("tests", optional=True)
@build_condition(PLATFORM="Linux-ARM")
def tests_arm(context):
    run_tests_remotely(context.env)

@build_step("publish", optional=True, default=False)
def publish(context):
    platform = context.env["PLATFORM"]
    version = context.options.publish_version or context.env.get("RELEASE_VERSION", "UNKNOWN")
    publishdir = context.env["OHOS_PUBLISH"]
    builddir = context.env["BUILDDIR"]

    filename = "ohos-{version}-{platform}.tar.gz".format(platform=platform, version=version)
    sourcepath = os.path.join(builddir, "ohos.tar.gz")
    targetpath = publishdir + '/' + filename
    scp(sourcepath, targetpath)

def run_tests_remotely(env):
    username = "root"
    host = "sheeva010.linn.co.uk"
    path = "~/ohosbuild/"
    target = username + "@" + host + ":" + path
    rsync(
        "-avz",
        "--delete",
        "--include='/dependencies/Linux-ARM/NUnit*'",
        "--include='/dependencies/Linux-ARM/'",
        "--exclude='/dependencies/Linux-ARM/*'",
        "--include='/dependencies/'",
        "--exclude='/dependencies/*'",
        "--include='/buildhudson/'",
        "--include='/wscript'",
        "--include='/waf'",
        "--include='/.lock-wafbuildhudson'",
        "--exclude='/*'",
        ".",
        target)
    #rsync(
    #    "-avz",
    #    env["OHDEVTOOLS_ROOT"]+"/remote_wrapper.py",
    #    target)

    # Note: huponexit
    #    This setting tells the shell to send SIGHUP (the hang-up signal) to
    #    all jobs when we disconnect. This means that we shouldn't have any
    #    orphaned processes when the CI server aborts a build due to time-out
    #    or user request. This should obviate the need for remote_wrapper.py.
    with SshSession(host, username) as ssh:
        ssh(
            ("shopt -s huponexit & LD_LIBRARY_PATH={path}buildhudson "+
            "'mono --debug "+
            "{path}dependencies/Linux-ARM/NUnit-2.5.10.11092/bin/net-4.0/nunit-console.exe "+
            "--labels --noshadow {path}buildhudson/*.Tests.dll'")
            .format(path=path))
