$ohos_scriptPath = $MyInvocation.MyCommand.Path
$ohos_scriptDir = Split-Path $ohos_scriptPath
$ohos_projectDir = Split-Path $ohos_scriptDir
$ohos_buildDir = Join-Path $ohos_projectDir "build"


function WithCwd($cwd, $action) {
    Push-Location $cwd
    [Environment]::CurrentDirectory = $PWD
    &$action
    Pop-Location
    [Environment]::CurrentDirectory = $PWD
}

function FromOhOsBuildDir($action) {
    WithCwd $ohos_buildDir $action
}

function ohos() {
    $ohosArgs = $args
    FromOhOsBuildDir {./ohOs.Host.exe $ohosArgs}
}

function waf() {
    ./waf $args
}

function ohos-install($appname) {
    ohos --install ($appname + ".zip")
}

function ohos-install-all() {
    dir $ohos_buildDir/*.zip | %{ ohos --install $_.FullName }
}

function ohos-remove-all {
    ohos --remove-all-apps
}

function ohos-setup {
    WithCwd $ohos_projectDir {
        ./go fetch
        ./waf configure
        ./waf build
    }
}

echo "ohOs aliases:"
echo "    ohos -> $ohos_buildDir/ohOs.Host.exe"
echo "    waf -> ./waf"
echo ""
echo "    ohos-install-all: Installs all apps from the build directory."
echo "    ohos-remove-all:  Remove all installed apps."
echo "    ohos-setup:       Fetch, configure, build."
