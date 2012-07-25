$().ready(function () {


    $('#txtMessage').bind('keypress', function (e) {
        if ((e.which && e.which == 13) || (e.keyCode && e.keyCode == 13)) {
            $('#btnMessage').click();
            return false;
        } else {
            return true;
        }
    });

    $('#btnMessage').bind('click', function (event) {
        xapp.tx($('#txtMessage').val());
        $('#txtMessage').val('');
    });

    $('#btnLogin').press(function () {
        $('#chatapp').data('ohj').navigateNext();
    });

    $('body').bind('xappevent', function (event, data) {
        console.log(data.type);
        console.log(data);
        console.log('----------------');
        switch (data.type) {
            case 'newtab':
                newtab(data);
                break;
            case 'closedtab':
                closedtab(data);
                break;
            case 'updatetab':
                updatetab(data);
                break;
            default:
                console.log(data);
        }

    });

});

function addsession(data) {
    if ($('#session_' + data.session).length == 0) {
        $('#sessionList').append($.fn.template($('#tplSession').html(),
        {
            session: data.session,
            tabcount: '0 tabs'
        }));
    }
}

function refreshQueuecount(data) {
    var sessiontab = data.session + '-' + data.tab;
    var queuecount = 0;
    var progressbar = $('#tab_' + sessiontab).find('.progress');
    if (data.queue >= 10) {
        queuecount = 10;
        progressbar.removeClass('progress-success').addClass('progress-danger');
    }
    else {
        queuecount = data.queue;
        progressbar.removeClass('progress-danger').addClass('progress-success');
    }
    progressbar.find('.bar').css({ 'width': (queuecount * 10) + '%' });
    $('#queue_' + sessiontab).html(data.queue)
}

function refreshTabcount(data) {
    var tabcount = $('#tablist_' + data.session + ' .tab').length;
    $('#tabcount_' + data.session).html(tabcount == 1 ? '1 tab' : tabcount + ' tabs');
}
function newtab(data) {
    addsession(data);

    var sessiontab = data.session + '-' + data.tab;
    $('#tablist_'+data.session).append($.fn.template($('#tplTab').html(),
        {
            app: data.app,
            queue: data.queue,
            tab: data.tab,
            sessiontab: sessiontab,
            user: data.user,
        }));
    refreshTabcount(data);
    refreshQueuecount(data) 
    $('#pg1').data('ohj').refreshPage();
}

function closedtab(data) {
    var sessiontab = data.session + '-' + data.tab;
    $('#tab_' + sessiontab).remove();

    var tabcount = $('#tablist_' + data.session + ' .tab').length;

    if (tabcount == 0) {
        $('#session_' + data.session).remove();
    }
    refreshTabcount(data);
    $('#pg1').data('ohj').refreshPage();
}

function updatetab(data) {
    var sessiontab = data.session + '-' + data.tab;
    if (data.reader) {
        $('#tab_' + sessiontab).addClass('busy');
    }else {
        $('#tab_' + sessiontab).removeClass('busy');
    }
    $('#user_' + sessiontab).text(data.user);
    refreshQueuecount(data);
}



// inline callbacks
function pageload(page) {
    page.data('ohj').refreshPage();
    
}

function goback(page) {
    $('#chatapp').data('ohj').navigateBack();
}

function showusers(page) {
    $('#chatapp').data('ohj').navigateNext();
}