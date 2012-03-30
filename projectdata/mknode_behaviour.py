# Build a node filesystem.

# Requires env vars NODE_VERSION and REPOSITORY to be set.
# Invoke with "NODE_VERSION=1.10~blah REPOSITORY=unstable go hudson_build mknode"

# Maintenance notes:
#
# See ci-build for the definition of special functions and classes used
# here, e.g. "@build_step", "rsync" and "SshSession".

username = "repo-incoming"
hostname = "image-builder.linn.co.uk"
image_types = ['main', 'sdk', 'fallback']
oh_rsync_user = "hudson-rsync"
oh_rsync_host = "openhome.org"

@build_step()
def push_dependencies(context):
    with SshSession(hostname, username) as ssh:
        ssh("sudo /bin/sh -c 'rm -rf image-builder/images/*'")
    rsync(
            "-avz", "--del",
            "image-builder",
            "%s@%s:/home/repo-incoming" % (username, hostname))

@build_step()
def generate_images(context):
    version = context.env["NODE_VERSION"]
    with SshSession(hostname, username) as ssh:
        for image_type in image_types:
            ssh("sudo /bin/sh -c 'cd image-builder && ./build.sh %s %s %s %s'" %(image_type,image_type,"openhome",version))

@build_step()
def publish_images(context):
    repo = context.env["REPOSITORY"]
    with SshSession(hostname, username) as ssh:
        for image_type in image_types:
            copy_mkdir = "sudo /bin/sh -c 'mkdir -p /var/www/openhome/%s/%s/'" %(repo, image_type)
            copy_kernel = "sudo /bin/sh -c 'cp -p oh-linux/uImage /var/www/openhome/%s/%s/%s.uImage'" %(repo, image_type, image_type)
            copy_update = "sudo /bin/sh -c 'cp -p image-builder/images/%s/update /var/www/openhome/%s/%s/'" %(image_type, repo, image_type)
            copy_ubifs = "sudo /bin/sh -c 'cp -p image-builder/images/%s/%s.ubi.img /var/www/openhome/%s/%s/'" %(image_type, image_type, repo, image_type)
            copy_version = "sudo /bin/sh -c 'cp -p image-builder/images/%s/version /var/www/openhome/%s/%s/'" %(image_type, repo, image_type)            
            publish_openhome = "sudo /bin/sh -c 'rsync -avz --del /var/www/openhome/%s/%s %s@%s:~/build/%s/node/'" %(repo, image_type, oh_rsync_user, oh_rsync_host, repo)
            # Note: This is directly taken from build_node.py, which doesn't use copy_mkdir.
            # I don't know if that's deliberate.
            ssh(copy_mkdir)
            ssh(copy_kernel)
            ssh(copy_update)
            ssh(copy_ubifs)
            ssh(copy_version)
            ssh(publish_openhome)
