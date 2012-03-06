import time

# Fail the build if ohDevTools is too old.
require_version(6)

arch_vars = ''
output = ""
ret = ''
host = "image-builder.linn.co.uk"
oh_rsync_user = "hudson-rsync"
oh_rsync_host = "openhome.org"
username = "repo-incoming"

# Command-line options. See documentation for Python's optparse module.
add_option("-a", "--artifacts", help="Build artifacts directory. Used to fetch dependencies.")
add_bool_option("--no-publish", help="Don't publish the packages.")

ALL_DEPENDENCIES = [
    "ohnet",
    "nunit",
    "ndesk-options",
    "moq",
    "yui-compressor",
    "sharpziplib",
    "log4net",
    "sshnet"]

# Unconditional build step. Process options to enable or
# disable parts of the build.
@build_step()
def process_optional_steps(context):
    if context.options.no_publish:
        select_optional_steps("-publish")

@build_step()
def set_arch_vars(context):
    all_arch_vars = {
            'arm' : {
                "setup" : "ls -al && export PATH=$PATH:/usr/local/arm-2010q1/bin && export CROSS_COMPILE=arm-none-linux-gnueabi- && export ARCH=arm",
                "compiler" : "dpkg-buildpackage -rfakeroot -us -uc -aarmel",
                "arch" : "armel",
                "OH_PLATFORM" : "Linux-ARM",
                },

            'linux-x86' : {
                "setup" : "ls -al",
                "compiler" : "dpkg-buildpackage -rfakeroot -us -uc",
                "arch" : "i386",
                "OH_PLATFORM" : "Linux-x86",
                },

            'linux-x64' : {
                "setup" : "ls -al",
                "compiler" : "dpkg-buildpackage -rfakeroot -us -uamd64",
                "arch" : "amd64",
                "OH_PLATFORM" : "Linux-x64",
                }
        }
    if "TARGET_ARCH" not in context.env:
        fail("Please specify TARGET_ARCH.")
    context.target = target = context.env["TARGET_ARCH"]
    if target not in all_arch_vars:
        fail("Unknown TARGET_ARCH: {0}".format(target))
    context.arch_vars = all_arch_vars[target]
    context.env["OH_PLATFORM"] = context.arch_vars["OH_PLATFORM"]
    print "selected target arch of:", target

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

# Principal build steps.
@build_step("fetch", optional=True)
def fetch(context):
    fetch_dependencies(ALL_DEPENDENCIES, platform=context.env["OH_PLATFORM"])

@build_step("configure", optional=True)
def configure(context):
    python("waf", "configure", context.configure_args)

@build_step()
def make_pkg(context):
    version = context.env["PACKAGE_VERSION"]
    shell('dch --newversion='+version+' < /bin/echo "automated hudson build"')
    shell(context.arch_vars["setup"] + "&&" + context.arch_vars["compiler"])

@build_step("publish", optional=True)
def publish_build(context): 
    print "running package publish"
    repo = context.env["REPOSITORY"]
    version = context.env["PACKAGE_VERSION"]

    rsync(
            '-avz',
            '../',
            '--include=*.tar.gz',
            '--include=*.deb',
            '--include=*.changes',
            '--include=*.dsc',
            '--exclude=*',
            '%s@%s:/var/www/openhome/apt-repo/incoming/%s' %(username,host,repo))

    cmd = "sudo /bin/sh -c 'cd /var/www/openhome/apt-repo && reprepro -Vb . include %s incoming/%s/ohos_%s_%s.changes'" %(repo, repo, version, context.arch_vars["arch"])
    publish_openhome = "sudo /bin/sh -c 'rsync -avz --del /var/www/openhome/apt-repo/ %s@%s:~/build/nightly/apt-repo'" %(oh_rsync_user, oh_rsync_host)
    #time.sleep(60 * random.random())
    # Don't see a good reason for this to be random. Try 20s for consistency.
    time.sleep(20.0)
    with SshSession(host, username) as ssh:
        ssh(cmd)
        ssh(publish_openhome)

