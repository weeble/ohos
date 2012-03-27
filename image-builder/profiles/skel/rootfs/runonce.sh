#!/bin/sh

# Set language env vars
export LANGUAGE=C
export LG_ALL=C
export LANG=C

# set env vars for suppressing dpkg interactive output.
export DEBIAN_FRONTEND=noninteractive
export DEBIAN_PRIORITY=critical

# pre-select some dpkg options (as per 'selections' file)
debconf-set-selections selections

# configure all packages
dpkg --configure -a

# update apt
apt-get update

# install additional packages (as per 'packages' file)
cat packages | xargs apt-get -qy install --allow-unauthenticated

# clean the package cache
apt-get clean

# set root password
echo root:openhome | chpasswd

# remove apt lists
rm -rf /var/lib/apt/lists

# move apt cache directories to RAM
echo 'Dir::State::lists "/tmp/apt/lists";' > /etc/apt/apt.conf.d/cache-dirs
echo 'Dir::Cache        "/tmp/apt/cache";' >> /etc/apt/apt.conf.d/cache-dirs

# stop processes started during package configuration
[ -x /etc/init.d/cron ] && /etc/init.d/cron stop
[ -x /etc/init.d/dbus ] && /etc/init.d/dbus stop

# remove the mirrors and enable the external debian repos
mv /etc/apt/sources.list.tmp /etc/apt/sources.list
rm -f /etc/apt/sources.list.d/multistrap-*.list

# let the DHCP client configure the DNS settings
[ -s /etc/default/dhcpcd ] && sed -i -e "s/^#* *SET_DNS=.*/SET_DNS=\'yes\'/" /etc/default/dhcpcd

# enable the startup scripts that disable the WDT and create dirs
insserv /etc/init.d/wdtstop
insserv /etc/init.d/makedirs

if [ -e /etc/watchdog.conf ] ; then
    mv /etc/watchdog.conf.ohos /etc/watchdog.conf
else
    rm -f etc/watchdog.conf.ohos
fi


