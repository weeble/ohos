# Source this script, e.g.:
#    . scripts/dev-aliases.sh

ohos_scriptPath=$MyInvocation.MyCommand.Path
ohos_scriptDir=`dirname $ohos_scriptPath`
ohos_projectDir=`basename $ohos_scriptDir`
ohos_buildDir=${ohos_projectDir}/build

ohos() {
    $ohos_buildDir/ohOs.Host.exe "$@"
}

waf() {
    ./waf $args
}

ohos-install($appname) {
    ohos --install ${ohos_buildDir}/${appname}.zip
}

ohos-install-all() {
    for i in $ohos_buildDir/*.zip
    do
        ohos --install $i
    done
}

ohos-remove-all() {
    ohos --remove-all-apps
}

ohos-setup() {
    pushd $ohos_projectDir
    ./go fetch && ./waf configure && ./waf build
    popd
}

echo "ohOs aliases:"
echo "    ohos -> $ohos_buildDir/ohOs.Host.exe"
echo "    waf -> ./waf"
echo ""
echo "    ohos-install-all: Installs all apps from the build directory."
echo "    ohos-remove-all:  Remove all installed apps."
echo "    ohos-setup:       Fetch, configure, build."
