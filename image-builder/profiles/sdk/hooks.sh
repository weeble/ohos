function hook_prof()
{
    cat ${PROFILE_PATH}/../fallback/packages    >>${IMG_ROOT}/packages
    cat ${PROFILE_PATH}/packages                >>${IMG_ROOT}/packages
}
