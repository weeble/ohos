function hook_skel()
{
    echo --running skeleton hook ...
    mkdir -p ${IMG_ROOT}/var/ohos
    mkdir -p ${IMG_ROOT}/opt/update

    echo --running \'${PROFILE}\' hook
    source ${PROFILE_PATH}/hooks.sh
    hook_prof
}
