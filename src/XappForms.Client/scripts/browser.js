;(function () {
    var getURLParameter = function(name) {
        return decodeURIComponent(
            (location.search.match(RegExp("[?|&]"+name+'=(.+?)(&|$)'))||[,null])[1]
        );  
    }

    var pathsegments = location.pathname.split('/').slice(1);
    var directorysegments = pathsegments.slice(0,-1);
    var filename = pathsegments[pathsegments.length-1];
    var appname = directorysegments[directorysegments.length-1];

    var appinfoRequest = new XMLHttpRequest();
    appinfoRequest.open('GET', '/apps/'+appname+'/', true);
    appinfoRequest.onload = function (e) {
        var mapping = JSON.parse(appinfoRequest.responseText).browserTypes;

        var istouch = 'ontouchstart' in window;
        //var pageName = window.location.pathname.substring(window.location.pathname.lastIndexOf('/') + 1);

        var ismobile = false, redirectPage = null;
        var istouch = 'ontouchstart' in window;
        if (screen.width <= '1000' && screen.height <= '1000') { ismobile = true; }
        var chooseRedirect = function(target) {
            if (typeof mapping === 'undefined') {
                return null; //'BROKEN.html';
            }
            relativeUrl = mapping[target];
            if (typeof relativeUrl === 'undefined') {
                relativeUrl = mapping['default'];
            }
            if (typeof relativeUrl === 'undefined') {
                relativeUrl = null; //'OTHER_BROKEN.html';
            }
            return relativeUrl;
        };
        if (!istouch) { redirectPage = chooseRedirect('desktop'); }
        else if (istouch && !ismobile) { redirectPage = chooseRedirect('tablet'); }
        else if (istouch && ismobile) { redirectPage = chooseRedirect('mobile'); }
        if(redirectPage != filename && getURLParameter('dev') != 'true' && redirectPage !== null)
            location.replace(redirectPage);
    };
    appinfoRequest.send();

})();
