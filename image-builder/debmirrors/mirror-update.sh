#! /bin/bash -xe

GERMBASE=/tmp/germ
PROFILE_PATH=../profiles

# Get command line options 
while getopts ":b:h" opt ; do
    case $opt in
    'h')
          show_help=1
          ;;
    'b')
          basedir=$OPTARG
          ;; 
    \?)
          echo "Invalid option -$OPTARG"
          exit 1
          ;;
    :)
          echo "Option -$OPTARG requires an argument"
          exit 1     
    esac 
done

if [ -n "$show_help" ] || [ -z "$basedir" ]; then
    echo "Usage: $0 -b <path>"
    exit 0
fi

# Create temporary directories for germinate
rm -rf $GERMBASE
mkdir $GERMBASE 
mkdir $GERMBASE/seeds
mkdir $GERMBASE/output 

# Create the germinate seeds
cat > $GERMBASE/seeds/STRUCTURE <<EOF
required:
supported:
EOF
echo > $GERMBASE/seeds/supported
echo > $GERMBASE/seeds/blacklist
echo ' * emdebian-archive-keyring [armel]' > $GERMBASE/required.seeds
cat $PROFILE_PATH/skel/rootfs/packages $PROFILE_PATH/main/packages | awk '/^[A-Za-z0-9]/ {print " * "$1" [armel]"}' >> $GERMBASE/required.seeds

echo > $basedir/debian/conf/node-packages.list
reprepro -V --noskipold -b $basedir/debian update unstable
reprepro -b $basedir/debian list unstable | awk '{print " * "$2" [armel]"}' >> $GERMBASE/required.seeds

sort $GERMBASE/required.seeds | uniq > $GERMBASE/seeds/required

# Run germinate to get all package dependencies
pushd $GERMBASE/output 
germinate -m http://ftp.uk.debian.org/debian -d wheezy -a armel -c main -s seeds -S file://$GERMBASE --no-rdepends
popd

# Create the package list file for reprepro
for pkg in `cat $GERMBASE/output/required | tail -n +3 | head -n -2 | cut -d'|' -f1`; do 
echo $pkg install 
done > $basedir/debian/conf/node-packages.list

# Now run em_autogrip to 'grip' the packages
em_autogrip -b $basedir --filter-name debian --noskipold
em_autogrip -b $basedir --filter-name debian

# Cleanup
rm -rf $GERMBASE
rm -f $basedir/debian/conf/node-packages.list

