@echo off
setlocal
set ohos_scriptsDir=%~dp0
set ohos_projectDir=%ohos_scriptsDir%..\
set ohos_buildDir=%ohos_projectDir%build\
set ohos_exe=%ohos_buildDir%ohOs.Host.Exe

echo %ohos_buildDir%
doskey ohos=%ohos_exe% $*
doskey ohos-install=%ohos_exe% --install %ohos_buildDir%$1.zip
doskey ohos-install-all=for %%I in (%ohos_buildDir%*.zip) do @%ohos_exe% --install %%I
doskey ohos-remove-all=%ohos_exe% --remove-all-apps
doskey go-fetch=go fetch
doskey waf-configure=waf configure

rem Hacks!
rem    We use 'for' to put multiple commands in one macro, because the $T separator
rem    results in messed up output with extra command-prompts in strange places.
rem    We use %%~I to expand the items and strip the quotes.
doskey ohos-setup=for %%I in (".\go fetch" ".\waf configure" ".\waf clean" ".\waf build") do @%%~I

echo ohOs aliases:
echo.    ohos -^> %ohos_buildDir%/ohOs.Host.exe
echo.    waf -^> ./waf
echo.
echo.    ohos-install-all: Installs all apps from the build directory.
echo.    ohos-remove-all:  Remove all installed apps.
echo.    ohos-setup:       Fetch dependencies, configure, clean and build.
