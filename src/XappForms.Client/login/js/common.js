
$().ready(function () {
    $.fn.decorateContainerPlugins($('body'));
    var users = {};

    $('#btnLogin').press(function () {
        //$('#chatapp').data('ohj').navigateNext();
        var userid = $('#cbxUsers option').not(function () { return !this.selected }).data('userid');
        if (typeof userid !== "undefined") {
            var msg = { 'type': 'user', 'id': userid };
            console.log('Login: ' + JSON.stringify(msg));
            xapp.tx(msg);
            $('#userlist > li').removeClass('active');
            $('#usr_' + userid).addClass('active');
        }
    });

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

    $('body').on('xappevent', function (event, data) {
        var message = '';
        switch (data.type) {
            case 'login':
                var alertMsg = 'You are now loginted,' + data.user.id + '!';
                alert(alertMsg);
                break;
            case 'user':
                var userSelection = $('#cbxUsers');
                var userTagId = 'user-' + data.userid;

                users[data.userid] = data.newValue;

                if (data.newValue === null && data.oldValue !== null) {
                    $('#' + userTagId).remove();
                }
                if (data.newValue !== null && data.oldValue === null) {
                    // A user we did not know about
                    $('<option />')
                        .attr('id', userTagId)
                        .data('userid', data.userid)
                        .text(data.newValue.displayName)
                        .appendTo(userSelection);
                }
                if (data.oldValue === null) {
                    // New user. We get this on startup.
                } else if (data.newValue === null) {
                    // User deleted. (Uncommon.)
                } else {
                    // Existing user changed state.
                    var oldName = data.oldValue.displayName;
                    var newName = data.newValue.displayName;
                    var oldStatus = data.oldValue.status;
                    var newStatus = data.newValue.status;
                }
                break;
            default:
                console.log(data);
        }

        //if (message != '') 
        //    $('<div/>')
        //            .text('Tab #' + data.sender + message)
        //            .addClass('message')
        //            .appendTo($('#pgConversation article > .content'));

    });

});


function goback(page) {
    $('#chatapp').data('ohj').navigateBack();
}

function showusers(page) {
    $('#chatapp').data('ohj').navigateNext();
}
