
// inline callbacks
function pageload(page) {
    switch (page.attr('id')) {
        case 'pgChat':
            {
                $('#pgUser').data('ohjpage').refreshPage();
                $('#pgConversation').data('ohjpage').refreshPage();
                setTimeout(function () {
                    $('#txtMessage').focus();
                }, 200);
                break;
            }
        default:
            {
                page.data('ohjpage').refreshPage();
            }
    }

}