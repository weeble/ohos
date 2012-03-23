import time
from glob import glob
from os.path import split
from uuid import uuid4
from os import remove

# Test debian packages.


# Fail the build if ohDevTools is too old.
require_version(9)

arch_vars = ''
output = ""
ret = ''
host = "image-builder.linn.co.uk"
oh_rsync_user = "hudson-rsync"
oh_rsync_host = "openhome.org"
username = "repo-incoming"

ssh_details = {
    'Linux-ARM' : dict(username='root', host='sheeva009.linn.co.uk', arch='armel'),
    'Linux-x86' : dict(username=None,   host=None,                   arch='i386'),
    'Linux-x64' : dict(username=None,   host=None,                   arch='amd64')
}

# Command-line options. See documentation for Python's optparse module.
#add_option("-a", "--artifacts", help="Build artifacts directory. Used to fetch dependencies.")
#add_bool_option("--no-publish", help="Don't publish the packages.")
add_option("--clean", action='append', dest='clean', help="Clean package directory. Follow with package directory. E.g. '--clean packages/are/here'")
add_option("--test", nargs=2, action='append', dest='test', help="Select a package to test. Follow with package directory and platform. E.g. '--test packages/are/here Linux-ARM'")
add_bool_option("--publish", help="On success, publish to openhome.org")
add_option("--repo", dest='repo', default='nightly', help="Select a repository.")

#ALL_DEPENDENCIES = [
    #"ohnet",
    #"nunit",
    #"ndesk-options",
    #"moq",
    #"yui-compressor",
    #"sharpziplib",
    #"log4net",
    #"sshnet"]


# Unconditional build step. Process options to enable or
# disable parts of the build.
@build_step()
def process_optional_steps(context):
    if context.options.clean:
        specify_optional_steps("clean")
    elif context.options.test:
        specify_optional_steps("pkgtest")
    if context.options.publish:
        modify_optional_steps("+publish")

@build_step("clean", optional=True, default=False)
def pkgtest(context):
    for pkgdir in context.options.clean:
        for pattern in ['*.deb', '*.changes', '*.tar.gz', '*.dsc']:
            for path in glob("{0}/{1}".format(pkgdir, pattern)):
                remove(path)
        #shell('rm -f "{0}/*.deb" "{0}/*.changes" "{0}/*.tar.gz" "{0}/*.dsc"'.format(pkgdir))

def globuniq(pattern):
    result = glob(pattern)
    if len(result) != 1:
        fail('Searching for exactly 1 file matching "{0}", but found {1}.'.format(pattern, len(result)))
    return result[0]

# Principal build steps.
@build_step("pkgtest", optional=True, default=True)
def pkgtest(context):
    for pkgdir, platform in context.options.test:
        settings = ssh_details[platform]
        username, host = settings['username'], settings['host']
        if host is None:
            print "Skipping package test for platform {0}, as no host specified.".format(platform)
            continue
        ohos_core = globuniq('{package_dir}/ohos-core_*.deb'.format(package_dir=pkgdir))
        ohos_appmanager = globuniq('{package_dir}/ohos-appmanager_*.deb'.format(package_dir=pkgdir))
        udn = str(uuid4())
        print "UDN: " + udn
        with SshSession(host, username) as ssh:
            path = "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
            ssh(path+' dpkg --purge ohos')
            ssh(path+' dpkg --purge ohos-distro')
            ssh(path+' dpkg --purge ohos-appmanager')
            ssh(path+' dpkg --purge ohos-core')
            ssh('rm -rf /var/ohos')
            ssh('rm -rf /etc/ohos')
            scp(ohos_core, '{username}@{host}:/root/'.format(username=username, host=host))
            scp(ohos_appmanager, '{username}@{host}:/root/'.format(username=username, host=host))
            ssh(path+' dpkg -i /root/{pkg}'.format(pkg=split(ohos_core)[1]))
            ssh(path+' dpkg -i /root/{pkg}'.format(pkg=split(ohos_appmanager)[1]))
            ssh('mkdir -p /etc/ohos/system-app.d/') # <- Shouldn't need to do this.
            # Note: trap below tries to make sure the ohos process is killed if our build
            # times out.
            conn = ssh.call_async("trap 'kill -HUP $(jobs -lp) 2>/dev/null || true' EXIT && {path} ohos --udn {udn} --subprocess nopipe".format(path=path, udn=udn))
            shell('mono build/ohOs.PackageTests.exe {udn}'.format(udn=udn))
            conn.send('exit\n')
            exitcode = conn.join()
            if exitcode != 0:
                fail("Node process failed during test: exitcode={0}".format(exitcode))

@build_step("publish", optional=True, default=False)
def publish_build(context): 
    print "running package publish"

    repo = context.options.repo

    for pkgdir, platform in context.options.test:
        settings = ssh_details[platform]
        arch = settings['arch']
        package_names = ['ohos', 'ohos-core', 'ohos-appmanager', 'ohos-distro']
        local_changes_paths = [globuniq('{package_dir}/{name}_*.changes'.format(name=name, package_dir=pkgdir)) for name in package_names]
        changes_files = [split(path)[1] for path in local_package_paths]

        rsync(
                '-avz',
                pkg_dir+'/',
                '--include=*.tar.gz',
                '--include=*.deb',
                '--include=*.changes',
                '--include=*.dsc',
                '--exclude=*',
                '%s@%s:/var/www/openhome/apt-repo/incoming/%s' %(username,host,repo))

        reprepro_cmd_template = "sudo /bin/sh -c 'cd /var/www/openhome/apt-repo && reprepro -Vb . include {repo} incoming/{repo}/{changes}"
        publish_openhome_cmd = "sudo /bin/sh -c 'rsync -avz --del /var/www/openhome/apt-repo/ %s@%s:~/build/nightly/apt-repo'" %(oh_rsync_user, oh_rsync_host)

        with SshSession(host, username) as ssh:
            for changes_file in changes_files:
                ssh(reprepro_cmd_template.format(repo=repo, changes=changes_file))
            ssh(publish_openhome_cmd)

