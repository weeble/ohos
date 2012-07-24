
$().ready(function () {
    $.fn.decorateContainerPlugins($('body'));
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
        $('#chatapp').data('ohj').navigateNext();
        var userid = $('#cbxUsers option').not(function () { return !this.selected }).data('userid');
        if (typeof userid !== "undefined") {
            var msg = { 'type': 'user', 'id': userid };
            console.log('Login: ' + JSON.stringify(msg));
            xapp.tx(msg);
            $('#userlist > li').removeClass('active');
            $('#usr_' + userid).addClass('active');
        }
    });

    var updateMessagePane = function () {
        $('#pgConversation').data('ohj').refreshPage();
        $('#pgConversation').data('ohj').getScroller().scrollToBottom();
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

    $('body').on('xappevent', function (event, data) {
        var message = '';
        switch (data.type) {
            //case 'connect':                             
            //    message = ' connected>';                             
            //    break;                             
            //case 'disconnect':                             
            //    message = ' disconnected>';                             
            //    break;                             
            case 'message':
                userMessage(data.sender, data.content);
                break;
            case 'login':
                $('#userlist > li').removeClass('active');
                $('#usr_' + data.user.id).addClass('active');
                $('#chatapp').data('ohj').navigateToPage($('#pgChat'));
                break;
            case 'user':
                var userSelection = $('#cbxUsers');
                var userTagId = 'user-' + data.userid;

                users[data.userid] = data.newValue;

                console.log("User event: " + data.userid + " " + (data.oldValue === null ? "new" : (data.newValue === null ? "gone" : "change")));

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

                    $('#userlist')
                        .data('ohj')
                        .addListItem($.fn.template($('#tplUser').html(), {
                            name: data.newValue.displayName,
                            status: data.newValue.status.toUpperCase(),
                            id: data.userid,
                            avatar: data.newValue.iconUrl
                        }));

                }
                if (data.oldValue === null) {
                    // New user. We get this on startup.
                    if (data.newValue.status === 'online') {
                        console.log('User added ' + data.userid);
                        systemMessage(data.newValue.displayName + ' is online. (userid=' + data.newValue.id + ')');
                        updateStatus(data.userid, data.newValue.status);
                    }
                } else if (data.newValue === null) {
                    // User deleted. (Uncommon.)
                    console.log('User deleted ' + data.userid);
                    systemMessage(data.oldValue.displayName + ' went offline. (userid=' + data.oldValue.id + ')');
                    updateStatus(data.userid, 'offline');
                } else {
                    // Existing user changed state.
                    var oldName = data.oldValue.displayName;
                    var newName = data.newValue.displayName;
                    var oldStatus = data.oldValue.status;
                    var newStatus = data.newValue.status;
                    if (oldName != newName) {
                        systemMessage(oldName + ' is now ' + newName);
                    }
                    if (oldStatus != newStatus) {
                        if (newStatus === "online") {
                            systemMessage(newName + ' came online.');
                        } else {
                            systemMessage(newName + ' went offline.');
                        }
                        updateStatus(data.userid, newStatus);
                    }
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


function changeuser(page) {
        xapp.tx({ 'type': 'changeuser' });
}

function goback(page) {
    $('#chatapp').data('ohj').navigateBack();
}

function showusers(page) {
    $('#chatapp').data('ohj').navigateNext();
}
