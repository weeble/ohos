function hook_prof()
{
    cat ${PROFILE_PATH}/packages >>${IMG_ROOT}/packages

    echo openhome-fallback > ${IMG_ROOT}/etc/hostname
    
    cp ${PROFILE_PATH}/rc.local ${IMG_ROOT}/etc/ -av
}
