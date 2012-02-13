var hit = 'click';

var nodeUdn = 'chrisc';
var applist;
var hasDownloads = false;
var hasApp = false;
var ghostApps = [];
var debugxml = '';
var debugprogressxml = '';
function appListAdded(data) {

	addApp(data);
}

function appListRemoved(id) {
	removeApp(id);
}

function appListChanged(id,data) {
    updateApp(id, data);
}


$().ready(function () {

    $('#debug').hide();
    $('.help').hide();
    $('#back').hide();
    $('#back').ohanimate();

    setTimeout(function () {
        $("#page-myapps .page-loader").hide();
        $("#page-appmanager .page-loader").hide();
        if (!hasApp)
            $('.help').show();
    }, 1700);
    /*** DEBUG ***/
    var debugadd = 0;
    $('#debug-add').bind('click', function () {
        debugxml = $("#actionxml").val();
        applist.appAdded(debugadd);
        debugadd++;
    });

    $('#debug-update').bind('click', function () {
        debugxml = $("#actionxml").val();
        applist.appChanged(debugadd);
    });

    $('#debug-progress').bind('click', function () {
        debugprogressxml = $("#progressxml").val();
        applist.appHasDownloads();
    });

    /*** ENDDEBUG ***/

    var _this = this;
    $('#app-add').bind('click', function () {
        $("#page-appmanager .page-loader").hide();

        $('#drawer').data('ohdrawer').showForm(
        {
            onSuccessFunction: function (input) {
                _this.applist.install(input, function () {
                    var ghostIndex = ghostApps.length;
                    var applauncher = parseTemplate($("#tpl_app_ghost").html(), {
                        url: input,
                        id: ghostIndex
                    });
                    hasApp = true;
                    $(".help").hide();
                    ghostApps.push(input);

                    $('.app-detailedlist').prepend(applauncher);
                    $('#ghostapp_' + ghostIndex).ohanimate({
                        animate: 'bounceIn'
                    });

                    setTimeout(function () {
                        $('#ghostloader_' + ghostIndex).ohloader({ loadingtext: 'Locating app...' });
                    }, 100);

                });
            },
            labelValue: 'Enter the App Url:',
            inputValue: 'http://'
        });
    });

    ohnet.subscriptionmanager.start(
    {
        allowWebSockets: true,
        startedFunction: function () {
            $('.app-list').html('');
            $('.app-detailedlist').html('');
            applist = new oh.app.applist(nodeUdn,
            {
                appListAddedFunction: appListAdded,
                appListRemovedFunction: appListRemoved,
                appListChangedFunction: appListChanged,
                appListUpdateProgressFunction: appListUpdateProgress,
                appListUpdateFailedFunction: appListUpdateFailed
            });

            if (isTouchDevice()) {
                hit = 'tap';
            }
            $('#back').bind(hit, function () {
                $('#pageScroller').data('ohpageslider').scrollToPage(0);
                $('#back').data('ohanimate').animate('fadeOut');
                setTimeout(function () {
                    $('#back').hide();
                }, 500);
            });

            $('.appmanager').bind(hit, function () {
                $('#pageScroller').data('ohpageslider').scrollToPage(1);
                $('#back').show();
                $('#back').data('ohanimate').animate('fadeIn');
            });
        }
    });
});

function appListUpdateProgress(appid, isGhost, progressPercent, progressBytes, totalBytes) {
    setTimeout(function () {
        var app;
      
        if (isGhost) {
            app = $('#ghostloader_' + ghostApps.indexOf(appid));
            app.ohloader();
            $('#ghostloader_' + ghostApps.indexOf(appid)).show();
        }
        else {
            app = $('#progress_' + appid);
            app.ohloader();
            $('#detailedapp_' + appid + ' .app-actions').hide();
            $('#progress_' + appid).show();
        }
        app.data('ohloader').setText('Downloading...');
        if (progressPercent) {
            if (progressPercent == 100)
                app.data('ohloader').setText('Installing...');

            app.data('ohloader').renderProgress();
            app.data('ohloader').updateProgress(progressPercent);
        }
        else {
            app.data('ohloader').renderSpinner();
        }
    }, 500);
}

function appListUpdateFailed(appid,isGhost) {
    if (isGhost) {
        var app = $('#ghostapp_' + ghostApps.indexOf(appid));
        app.data('ohanimate').animate('bounceOut');
        setTimeout(function () {
            app.remove();
            $('#drawer').data('ohdrawer').showError('App failed to download');
        }, 500);
        ghostApps[ghostApps.indexOf(appid)] = null;
    }
    else {
        $('#detailedapp_' + appid).data('ohanimate').animate('bounceIn');
        $('#detailedapp_' + appid + ' .app-actions').show();
        $('#progress_' + appid).hide();
        $('#drawer').data('ohdrawer').showError('App failed to update');
    }
}


function updateApp(id, app) {
    
    $('#app_' + id + ' .text').html(app.name);

    if (app.updateStatus && app.updateStatus == "available") {
        $('#detailedapp_' + app.id + ' .btn-app-update').show();
	
    } 


    $('#detailedapp_' + id + ' .app-name').html(app.name);
    $('#detailedapp_' + id + ' .app-version').html('Version: ' +app.version);
    $('#detailedapp_' + id + ' .app-description').html(app.description);
    
    $('#app_' + id).data('ohanimate').animate('bounceIn');
    $('#detailedapp_' + id).data('ohanimate').animate('bounceIn');

    $('#detailedapp_' + id + ' .app-actions').show();
    $('#progress_' + id ).hide();
   
}

function addApp(app) {
    
    $("#page-myapps .page-loader").hide();
    $("#page-appmanager .page-loader").hide();
    hasApp = true;
    $(".help").hide();
    var ghost = ghostApps.indexOf(app.url);
    if (ghost != -1) {
        $("#ghostapp_" + ghost).remove();
        ghostApps[ghost] = null;
    }
    var applauncher = parseTemplate($("#tpl_app-launcher").html(), {
	    id: app.id,
		name : app.name
	});
	var appmanager = parseTemplate($("#tpl_app-manager").html(), {
	    id: app.id,
	    handle: app.handle,
		name : app.name,
		version : app.version,
        description: app.description
	});
    $('.app-list').append(applauncher);
    $('#app_' + app.id).bind(hit, function () {
        window.location = 'http://www.openhome.org';
    });
    var ghostIndex = ghostApps.indexOf(app.url);
    if (ghostIndex != -1) {
        ghostApps[ghostIndex] = null;
        $("#ghostapp_" + ghostIndex).after(appmanager);
        $("#ghostapp_" + ghostIndex).remove();
    }
    else {
        $('.app-detailedlist').append(appmanager);
    }
	$('#detailedapp_' + app.id + ' .btn-app-update').hide();
	$('#app_' + app.id).ohanimate({
		animate : 'bounceIn'
	});

    $('#detailedapp_' + app.id + ' .btn-app-remove').ohanimate({
		speed : 1000
	});
    $('#detailedapp_' + app.id).ohanimate({
		animate : 'bounceIn'
	});

	$('#detailedapp_' + app.id + ' .btn-app-remove').bind(hit, function () {
	    $('#drawer').data('ohdrawer').showWarning(
        {
            onSuccessFunction: function () {
                applist.remove(app.id, function () {
                    removeApp(app.id, app.name);
                });
            }
        });
	    return false;
	});

	$('#detailedapp_' + app.id + ' .btn-app-update').bind(hit, function () {
	    appListUpdateProgress(app.id, false);
	    return false;
	});
	if (ghost != -1) {
	    $("#drawer").data("ohdrawer").showSuccess('App has been installed');
	}   
//$('#progress_' + app.id).ohprogressbar({
//		speed : 2000
//	});

}

function removeApp(appid,appname) {
    var app = $('#app_' + appid);
   
	app.data('ohanimate').animate('bounceOut');

	var detailedapp = $('#detailedapp_' + appid);
	detailedapp.data('ohanimate').animate('bounceOut');
	setTimeout(function() {
		app.remove();
		detailedapp.remove();
		$('#drawer').data('ohdrawer').showSuccess(appname + ' has been removed');
}, 500);


}

function removeGhostApp(appid) {
    var app = $('#ghostapp_' + appid);
    app.data('ohanimate').animate('bounceOut');
    setTimeout(function () {
        app.remove();
    }, 500);
}

function isTouchDevice() {
	return 'ontouchstart' in window;
}
