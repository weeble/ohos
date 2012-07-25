
// inline callbacks
function pageload(e, page) {
    page = $(page);
    switch (page.attr('id')) {
        case 'pgChat':
            {
                console.log($('#pgUser').data('ohj'));
                $('#pgUser').data('ohj').refreshPage();
                $('#pgConversation').data('ohj').refreshPage();
                setTimeout(function () {
                    $('#txtMessage').focus();
                }, 200);
                break;
            }
        default:
            {
                page.data('ohj').refreshPage();
            }
    }

}