
// inline callbacks
function pageload(e, page) {
    page = $(page);
    switch (page.attr('id')) {
        case 'pgChat':
            {
                setTimeout(function () {
                    $('#txtMessage').focus();
                }, 200);
                break;
            }
        default:
            {
            }
    }

}