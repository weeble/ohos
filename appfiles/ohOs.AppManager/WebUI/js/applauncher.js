var hit = 'click';

var applist;
var hasDownloads = false;
var hasApp = false;
var ghostApps = [];
var debugxml = '';
var debugprogressxml = '';
function appListAdded(data) {
	addApp(data);
}

function appListRemoved(handle,data) {
	removeApp(handle,data);
}

function appListChanged(handle,data) {
    updateApp(handle, data);
}


$().ready(function () {

    $('.help').hide();
    $('#back').hide();
    $('#back').ohanimate();

    setTimeout(function () {
        $("#page-myapps .page-loader").hide();
        $("#page-appmanager .page-loader").hide();
        if (!hasApp)
            $('.help').show();
    }, isRemote ? 10000 : 2000);


    var _this = this;
    $('#app-add').bind('click', function () {
        $("#page-appmanager .page-loader").hide();

        $('#drawer').data('ohdrawer').showForm(
        {
            onSuccessFunction: function (input) {
                _this.applist.install(input);
            },
            labelValue: 'Enter the App Url:',
            inputValue: 'http://'
        });
    });

    ohnet.subscriptionmanager.start(
    {
    	disconnectedFunction: function() {
    		setTimeout(function() {
    			$('body').data('ohapp').restart('Restarting your hub, please wait...');
    		},0);
    	},
        allowWebSockets: true,
        debugMode: false,
        startedFunction: function () {
        	$('body').ohapp({
        		displayAppManagerLink:false
        		,checkForSystemUpdate:false,
        		restartWait:5000
        	});
            $('.app-list').html('');
            $('.app-detailedlist').html('');
            applist = new ohapp.applist(nodeUdn,
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


function addGhostApp(input)
{

	var ghostIndex = ghostApps.length;
    var applauncher = ohtemplate.parse($("#tpl_app_ghost").html(), {
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
}



function appListUpdateProgress(handle, isGhost, url,progressPercent, progressBytes, totalBytes) {
        var app;
        if (isGhost) {
        	
        	var index = ghostApps.indexOf(url);
        	if(index== -1)
        	{
        		addGhostApp(url);
        		index = ghostApps.length;
        	}
            app = $('#ghostloader_' + index);
            app.ohloader();
            $('#ghostloader_' + index).show();
        }
        else {
            app = $('#progress_' + handle);
            app.ohloader();
            $('#detailedapp_' + handle + ' .app-actions').hide();
            $('#progress_' + handle).show();
        }
        if(app.data('ohloader'))
        {
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
        }

}

function appListUpdateFailed(handle,isGhost) {
    if (isGhost) {
        var app = $('#ghostapp_' + ghostApps.indexOf(handle));
        app.data('ohanimate').animate('bounceOut');
        setTimeout(function () {
            app.remove();
            $('#drawer').data('ohdrawer').showError('App failed to download');
        }, 500);
        ghostApps[ghostApps.indexOf(handle)] = null;
    }
    else {
        $('#detailedapp_' + handle).data('ohanimate').animate('bounceIn');
        $('#detailedapp_' + handle + ' .app-actions').show();
        $('#progress_' + handle).hide();
        $('#drawer').data('ohdrawer').showError('App failed to update');
    }
}


function updateApp(handle, app) {
    $('#app_' + handle + ' .text').html(app.name);

    if (app.updateStatus && app.updateStatus == "available") {
        $('#detailedapp_' + handle + ' .btn-app-update').show();
    } 


    $('#detailedapp_' + handle + ' .app-name').html(app.name);
    $('#detailedapp_' + handle + ' .app-version').html('Version: ' +app.version);
    $('#detailedapp_' + handle + ' .app-description').html(app.description);
    
    $('#app_' + handle).data('ohanimate').animate('bounceIn');
    $('#detailedapp_' + handle).data('ohanimate').animate('bounceIn');

    $('#detailedapp_' + handle + ' .app-actions').show();
    $('#progress_' + handle ).hide();
   
}

function addApp(app) {
    $("#page-myapps .page-loader").hide();
    $("#page-appmanager .page-loader").hide();
    hasApp = true;
    $(".help").hide();

    var ghost = ghostApps.indexOf(app.updateUrl);

    if (ghost != -1) {
        $("#ghostapp_" + ghost).remove();
        ghostApps[ghost] = null;
    }

    var applauncher = ohtemplate.parse($("#tpl_app-launcher").html(), {
	    id: app.id,
	    handle: app.handle,
		name : app.friendlyName,
		iconUri : app.iconUri
	});
	var appmanager = ohtemplate.parse($("#tpl_app-manager").html(), {
	    id: app.id,
	    handle: app.handle,
		name : app.friendlyName,
		version : app.version,
        description: app.description,
		iconUri : app.iconUri
	});
    $('.app-list').append(applauncher);
    $('#app_' + app.handle).bind(hit, function () {
        window.location = app.url;
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
	$('#detailedapp_' + app.handle + ' .btn-app-update').hide();
	
	
	$('#app_' + app.handle).ohanimate({
		animate : 'bounceIn'
	});
    $('#detailedapp_' + app.handle).ohanimate({
		animate : 'bounceIn'
	});
	
    $('#detailedapp_' + app.handle + ' .btn-app-remove').ohanimate({
		speed : 1000
	});


	$('#detailedapp_' + app.handle + ' .btn-app-remove').bind(hit, function () {
	    $('#drawer').data('ohdrawer').showWarning(
        {
            onSuccessFunction: function () {
   
            	var pro = $('#progress_' + app.handle);
            	$('#detailedapp_' + app.handle + ' .btn-app-remove').hide();
            	$('#detailedapp_' + app.handle + ' .btn-app-update').hide();
            	pro.ohloader();
            	pro.data('ohloader').setText("Deleting...");
            	
                applist.remove(app.handle);
            }
        });
	    return false;
	});

	$('#detailedapp_' + app.handle + ' .btn-app-update').bind(hit, function () {
		applist.update(app.handle,function() {
			appListUpdateProgress(app.handle, false);
		});
	    return false;
	});
	if (ghost != -1) {
	    $("#drawer").data("ohdrawer").showSuccess('App has been installed');
	}   
	
	if (app.updateStatus && app.updateStatus == "available") {
        $('#detailedapp_' + app.handle + ' .btn-app-update').show();
	    } 

}

function removeApp(handle,appdata) {
    var app = $('#app_' + handle);

	app.data('ohanimate').animate('bounceOut');
	var detailedapp = $('#detailedapp_' + handle);
	detailedapp.data('ohanimate').animate('bounceOut');
	setTimeout(function() {
		app.remove();
		detailedapp.remove();
		$('#drawer').data('ohdrawer').showSuccess((appdata.friendlyName == null ? 'App' : appdata.friendlyName) + ' has been removed');
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
