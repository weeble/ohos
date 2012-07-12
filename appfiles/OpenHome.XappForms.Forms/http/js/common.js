
$().ready(function () {

    var users = {};


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
        $('#chatapp').data('ohjpageslider').navigateNext();
        var userid = $('#cbxUsers option').not(function () { return !this.selected; }).data('userid');
        if (typeof userid !== "undefined") {
            var msg = { 'type': 'user', 'id': userid };
            console.log('Login: ' + JSON.stringify(msg));
            xapp.tx(msg);
            $('#userlist > li').removeClass('active');
            $('#usr_' + userid).addClass('active');
        }
    });

    var updateMessagePane = function () {
        $('#pgConversation').data('ohjpage').refreshPage();
        $('#pgConversation').data('ohjpage').getScroller().scrollToBottom();
    };



    var updateStatus = function (usrid, status) {
        var statusElement = $('#usr_' + usrid + ' .status');
        statusElement.html(status.toUpperCase());
        if (status == 'online')
            statusElement.addClass('label-success');
        else
            statusElement.removeClass('label-success');
    };

    var systemMessage = function (message) {
        $('<div/>')
                .text('<' + message + '>')
                .addClass('message')
                .addClass('system-message')
                .appendTo($('#pgConversation article > .content'));
        updateMessagePane();
    };

    var userMessage = function (userid, message) {
        $('#pgConversation article > .content').append($.fn.template($('#tplMessage').html(), { name: userid, message: message, avatar: users[userid].iconUrl }));
        updateMessagePane();
    };

    //    var systemMessage = function (message) {
    //        $('<div/>')
    //                .text('<' + message + '>')
    //                .addClass('message')
    //                .addClass('system-message')
    //                .appendTo($('#pgConversation article > .content'));
    //        updateMessagePane();
    //    };

    //    var userMessage = function (userid, message) {
    //        var msgDiv = $('<div/>')
    //            .addClass('message');
    //        var senderSpan = $('<span/>')
    //            .addClass('sender')
    //            .text(userid + ':')
    //            .appendTo(msgDiv);
    //        msgDiv.append(' ');
    //        var messageSpan = $('<span/>')
    //            .text(message)
    //            .appendTo(msgDiv);
    //        msgDiv.appendTo($('#pgConversation article > .content'));
    //        updateMessagePane();
    //    };

    var controlClasses = {
        'grid' : { 'template' : 'xf-grid' },
        'button' : { 'template' : 'xf-button' }
    };

    var controls = {};

    var xappTmpl = function(template, id, slots) {
        var element = template.clone();
        if (id) {
            element.attr('id',id);
        } else {
            element.removeAttr('id');
        }
        slots = slots || {};
        for (var key in slots) {
            element.find('.xfslot-'+key).replaceWith(slots[key]);
        }
        return element;
    };

    function handle_xf_control(data) {
        var controlClass = controlClasses[data['class']];
        var template = controlClass.template;
        controls[data.id] = $('#'+template).clone().attr('id','xf-'+data.id);
    }

    function handle_xf_slot(data) {
        var parentControl;
        if (data['parent'] === 0) {
            parentControl = $('body');
        } else {
            parentControl = controls[data['parent']];
        }
        var childControl = controls[data['child']];
        parentControl.find('.xfslot-'+data.slot).replaceWith(childControl);
    }

    function handle_xf_property(data) {
        var control = controls[data.control];
        // TODO: Fix this button-specific hack.
        if (data.property === 'text') {
            control.text(data.value);
        }
    }

    function handle_xf_subscribe(data) {
        var control = controls[data.control];
        control.bind(data['event'], function () { xapp.tx({ 'type': 'xf-event', 'control': data.control, 'event':data['event'], 'object': 'PLACEHOLDER'}); });
    }


    $('body').on('xappevent', function (event, data) {
        var message = '';
        switch (data.type) {
            //case 'connect':                            
            //    message = ' connected>';                            
            //    break;                            
            //case 'disconnect':                            
            //    message = ' disconnected>';                            
            //    break;                            
            case 'xf-control': handle_xf_control(data); break;
            case 'xf-slot': handle_xf_slot(data); break;
            case 'xf-property': handle_xf_property(data); break;
            case 'xf-subscribe': handle_xf_subscribe(data); break;
            default:
                console.log(data);
                break;
        }

        //if (message != '') 
        //    $('<div/>')
        //            .text('Tab #' + data.sender + message)
        //            .addClass('message')
        //            .appendTo($('#pgConversation article > .content'));

    });

});


function changeuser(page) {
        xapp.tx({ 'type': 'changeuser' });
}

function goback(page) {
    $('#chatapp').data('ohjpageslider').navigateBack();
}

function showusers(page) {
    $('#chatapp').data('ohjpageslider').navigateNext();
}
