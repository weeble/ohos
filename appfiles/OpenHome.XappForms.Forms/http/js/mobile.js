
// inline callbacks
function pageload(e, page) {
    page = $(page);
    switch (page.attr('id')) {
        case 'pgChat':
            {
                $('#pgUser').data('ohjpage').refreshPage();
                $('#pgConversation').data('ohjpage').refreshPage();
                break;
            }
        default:
            {
                page.data('ohjpage').refreshPage();
            }
    }

}