;(function () {
    $().ready(function () {
        var xapp = window.xapp = {};
        var pathsegments = location.pathname.split('/').slice(1);
        var directorysegments = pathsegments.slice(0,-1);
        var filename = pathsegments[pathsegments.length-1];
        var appname = directorysegments[directorysegments.length-1];

        var appname = xapp.appname = location.pathname.split('/')[1];
        var sessionid = xapp.sessionid = readCookie('XappSession');

        var handleLongPoll;
        var startLongPoll;
        var handleTabCreated;
        var handleTabError;
        var longPollUrl;
        var tabid;
        var stopped = xapp.stopped = false;
        handleLongPoll = function (data) {
            // First check for errors:
            for (var index in data)
            {
                if (data[index].type == 'error') {
                    // Treat as a disconnect.
                    // TODO: Handle error.
                    return;
                }
            }
            if(!xapp.stopped)
                startLongPoll();
            
            for (var index in data)
            {
              
                if (data[index].type == 'event') {
                    $('body').trigger('xappevent', data[index].value);
                }
            }
        };
        handleLongPollError = function (longPollValue, error, exc) {
            $('body').trigger('xappevent', { type: 'error', value: error });
            // TODO: Exponential backoff on failures.
            setTimeout(createTab, 5000);
        };
        handleTabCreated = function (data) {
            longPollUrl = data.tabUrl;
            tabid = xapp.tabid = data.tabId;
            startLongPoll();
            $('body').trigger('xappready');
                  
        };
        handleTabError = function (event, error, exc) {
            $('body').trigger('xappevent', { type: 'error', value: error });
            // TODO: Exponential backoff on failures.
            setTimeout(createTab, 5000);
        }
        startLongPoll = function () {
            setTimeout(function() {
                $.ajax({
                    url: longPollUrl,
                    cache: false,
                    dataType:'json',
                    success: handleLongPoll,
                    error: handleLongPollError
                }); 
            },10);
        };

        xapp.stopLongPoll = function () {
            xapp.stopped = true;
        };

        createTab = function () {
            $.ajax({
                url: '/poll/' + sessionid + '/?appname=' + appname,
                type: 'POST',
                cache: false,
                dataType:'json',
                success: handleTabCreated,
                error: handleTabError
            });
        };

        createTab();


        var handleTxSuccess = function (data) {
        //    console.log("TX successful.");
        };

        var handleTxFailure = function (event, error, exc) {
            console.log("TX failed: " + error);
        };

        xapp.tx = function (data) {
                $.ajax({
                    url: '/send/' + sessionid + '/' + tabid,
                    type: 'POST',
                    success: handleTxSuccess,
                    error: handleTxFailure,
                    data: JSON.stringify(data)
                });
        };

    });

    readCookie = function(name) {
        var nameEQ = name + "=";
        var ca = document.cookie.split(';');
        for (var i = 0; i < ca.length; i++) {
            var c = ca[i];
            while (c.charAt(0) == ' ') c = c.substring(1, c.length);
            if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length, c.length);
        }
        return null;
    };

})();
