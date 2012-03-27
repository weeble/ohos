#! /bin/bash -xe

EXECUTABLE=`pwd`/$0
IMG_NAME=$1
PROFILE=${2:-main}
HOSTNAME=${3:-openhome}
VERSION=${4:-no-version}

EXEC_PATH=`dirname ${EXECUTABLE}`
IMG_PATH=${EXEC_PATH}/images/${IMG_NAME}
PROFILE_PATH=${EXEC_PATH}/profiles/${PROFILE}
OH_LINUX_PATH=${EXEC_PATH}/../oh-linux

IMG_ROOT=${IMG_PATH}/rootfs
IMG_TARBALL=${IMG_PATH}/${IMG_NAME}.tgz
IMG_UBIFS=${IMG_PATH}/${IMG_NAME}.ubifs.img
IMG_UBI=${IMG_PATH}/${IMG_NAME}.ubi.img
IMG_UBI_CFG=${IMG_PATH}/ubi.cfg
IMG_KERNEL=${IMG_PATH}/${PROFILE}.uImage

# Import profile hooks - these are functions for processing the default
# installation
source profiles/skel/hooks.sh

if [ -e multistrap-${PROFILE}.cfg ]; then
    MULTISTRAP_CFG=multistrap-${PROFILE}.cfg
else
    MULTISTRAP_CFG=multistrap.cfg
fi

#######################################################################
# Build up the rootfs as far as possible before requiring
# chroot for some configuration

# Build the multistrap image
multistrap -f ${MULTISTRAP_CFG} -d ${IMG_ROOT}

# Add skeleton configuration files
cp -av --no-preserve=ownership profiles/skel/rootfs/* ${IMG_ROOT}

# Add the kernel modules
if [ -d ${OH_LINUX_PATH} ]; then
    cp -P -R --preserve=mode,timestamps,links -v ${OH_LINUX_PATH}/modules/* ${IMG_ROOT}
    cp --preserve=mode,timestamps -v ${OH_LINUX_PATH}/uImage ${IMG_KERNEL}
fi

# Insert hostname
echo ${HOSTNAME} > ${IMG_ROOT}/etc/hostname

# Run profile hook for adding/modifying files in the rootfs
hook_skel

# Create a version file
echo ${VERSION} >${IMG_PATH}/version
cp ${IMG_PATH}/version ${IMG_ROOT}

# Create a default /dev
tar -xzf /usr/share/debootstrap/devices.tar.gz -C ${IMG_ROOT}

#######################################################################
# Ready to chroot

# Add qemu-arm-static so we can run while chrooted
cp `which qemu-arm-static` ${IMG_ROOT}/usr/bin

echo --mounting ...
sleep 2

# Mount proc
mount -t proc proc ${IMG_ROOT}/proc

# Enter chroot and run the 'runonce.sh' script, which installs
# packages that multistrap cannot.
chroot ${IMG_ROOT} /runonce.sh

echo --unmounting ...

umount ${IMG_ROOT}/proc

rm ${IMG_ROOT}/usr/bin/qemu-arm-static
rm ${IMG_ROOT}/runonce.sh 
rm ${IMG_ROOT}/packages
rm ${IMG_ROOT}/selections

#######################################################################
# Create output images

# tarball
pushd ${IMG_ROOT}
tar -czf ${IMG_TARBALL} *
popd

# Make UBIFS image
mkfs.ubifs -q -r ${IMG_ROOT} -m 2048 -e 129024 -c 4096 -o ${IMG_UBIFS}

# create ubi config
cat <<EOF >${IMG_UBI_CFG}
[ubifs]
mode=ubi
vol_id=0
vol_type=dynamic
vol_name=rootfs
vol_flags=autoresize
EOF

echo image=${IMG_UBIFS} >> ${IMG_UBI_CFG}

# Create binary ubi image
ubinize -m 2048 -p 128KiB -s 512 -o ${IMG_UBI}  ${IMG_UBI_CFG}

# Install the update script
sedscr="s/__ROOTFS_SIZE_VAL__/`stat -c %s ${IMG_UBI}`/;"
sedscr="${sedscr}s/__ROOTFS_CHKSUM_VAL__/`md5sum -b ${IMG_UBI} | awk '{print $1}'`/;"
if [ -s ${IMG_KERNEL} ]; then
    sedscr="${sedscr}s/__KERN_SIZE_VAL__/`stat -c %s ${IMG_KERNEL}`/;"
    sedscr="${sedscr}s/__KERN_CHKSUM_VAL__/`md5sum -b ${IMG_KERNEL} | awk '{print $1}'`/;"
else
    sedscr="${sedscr}s/__KERN_SIZE_VAL__/0/;"
    sedscr="${sedscr}s/__KERN_CHKSUM_VAL__//;"
fi
sed -e "$sedscr" ${PROFILE_PATH}/update > ${IMG_PATH}/update
chmod +x ${IMG_PATH}/update

