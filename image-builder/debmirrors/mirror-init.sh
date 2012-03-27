#! /bin/bash -e

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

mkdir -p $basedir

for d in debian grip locale ; do
    rm -rf $basedir/$d
    cp -a repos/$d $basedir
    reprepro -b $basedir/$d export
    reprepro -b $basedir/$d createsymlinks
done

echo > $basedir/debian/conf/pkglist
