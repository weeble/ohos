
/**
*
* Represents a Device Proxy
* 
*/

/**
@namespace oh.widget.devices
*/
oh.namespace('oh.app');

oh.app.applist = function (node, options) {
    var defaults = {
        appReadyFunction: null,
        appListAddedFunction: null,
        appListRemovedFunction: null,
        appListChangedFunction: null,
        appListUpdateProgressFunction: null,
        appListUpdateFailedFunction: null,
        queryProgressInterval: 1000
    }

    options = ohnet.util.mergeOptions(defaults, options);

    this.appReadyFunction = options.appReadyFunction;
    this.appListAddedFunction = options.appListAddedFunction;
    this.appListRemovedFunction = options.appListRemovedFunction;
    this.appListChangedFunction = options.appListChangedFunction;
    this.appListUpdateProgressFunction = options.appListUpdateProgressFunction;
    this.appListUpdateFailedFunction = options.appListUpdateFailedFunction;

    this.queryProgressInterval = options.queryProgressInterval;
    this.hasDownloads = false;
    this.downloadPoll = false;

    this.list = {};
    this.appIds = [];
    this.appProxy = new CpProxyOpenhomeOrgAppManager1(node);
    this.appProxy.subscribe(function () {
        if (_this.appReadyFunction)
            _this.appReadyFunction();
    });
    this.setupArrayIdChanged();
    this.setupSequenceNumberChanged();

};

oh.app.applist.prototype.setupArrayIdChanged = function () {
    try {
        var _this = this;
        this.appProxy.AppHandleArray_Changed(function (state) {
            var newAppIds = oh.util.convert.byteStringToUint32Array(state);
            _this.appIds.sort(function (a, b) { return a - b });
            _this.appIds.diff(newAppIds
                                    , _this.appAdded
                                    , _this.appRemoved);
            _this.appIds = newAppIds;
        }, this);
    }
    catch (e) {
        oh.logError('oh.app.applist.prototype.setupArrayIdChanged: ' + e.message);
    }
}

oh.app.applist.prototype.setupSequenceNumberChanged = function () {
    try {

        var _this = this;
        this.appProxy.AppSequenceNumberArray_Changed(
        function (state) {
            _this.appSeqNums = oh.widget.util.hasSeqNumChanged(_this.appSeqNums, state, _this.appIds, _this.appChanged);
        });
    }
    catch (e) {
        oh.logError('SequenceNumberArray_Changed: ' + e.message);
    }
}

oh.app.applist.prototype.appHasDownloads = function () {
    var _this = this;
    if (!this.downloadPoll) {
        this.downloadPoll = true;
        debugprogressxml = $("#progressxml").val();  // debug
        this.appProxy.GetAllDownloadsStatus(function (result) {
            _this.hasDownloads = false;
            var xml = result.DownloadStatusXml;
            var downloadListObj = oh.util.dataformat.xmlStringToJson(xml);
            for (var d in downloadListObj) {
                var download = downloadListObj[d].download;
                if (download.appHandle && download.status == 'downloading') {
                    if (_this.appListUpdateProgressFunction)
                        _this.appListUpdateProgressFunction(download.appId, false, download.progressPercent, download.progressBytes, download.totalBytes);
                    _this.hasDownloads = true;
                    // app update
                } else if (download.url && download.status == 'downloading') {
                    if (_this.appListUpdateProgressFunction)
                        _this.appListUpdateProgressFunction(download.url, true, download.progressPercent, download.progressBytes, download.totalBytes);
                    _this.hasDownloads = true;
                }
                else {
                    if (_this.appListUpdateFailedFunction) {
                        if (download.appHandle)
                            _this.appListUpdateFailedFunction(download.appId, false);
                        else if (download.url)
                            _this.appListUpdateFailedFunction(download.url, true);
                    }
                }
            }
            if (_this.hasDownloads) {
                setTimeout(function () {
                    _this.downloadPoll = false;
                    _this.appHasDownloads();
                }, _this.queryProgressInterval);
            }
            else {
                _this.downloadPoll = false;
            }

            // for (var appSeq in this.list) {
            //       if (this.list.hasOwnProperty(appSeq)) {
            //           if (this.list[appSeq].updateStatus == 'downloading') {
            //               this.hasDownloads = true;
            //           }
            ///       }
            //    }
            //   if (this.hasDownloads) {


        });
    }
    //       if (!this.downloadStatusPoll) {

    //           this.downloadStatusPoll = setTimeout(function () {
    //               console.log('check downloads');
    //           }, this.queryProgressInterval);
    //       }
}

oh.app.applist.prototype.appAdded = function (seq) {
    var _this = this;

    this.appProxy.GetAppStatus(seq, function (result) {
        var xml = result.AppListXml;
        var appListObj = oh.util.dataformat.xmlStringToJson(xml);

        if (appListObj.appList.app) {
            var handle = appListObj.appList.app.handle;
            _this.list[handle] = appListObj.appList.app;
            if (_this.appListAddedFunction)
                _this.appListAddedFunction(_this.list[handle]);
            _this.appHasDownloads();
        }
    });

}

oh.app.applist.prototype.appChanged = function (seq) {
    var _this = this;

    this.appProxy.GetAppStatus(seq, function (result) {
        var xml = result.AppListXml;
        var appListObj = oh.util.dataformat.xmlStringToJson(xml);

        if (appListObj.appList.app) {
            var handle = appListObj.appList.app.handle;
            var oldid = _this.list[handle].id;
            _this.list[handle] = appListObj.appList.app;

            if (_this.appListChangedFunction)
                _this.appListChangedFunction(oldid, _this.list[handle]);
            _this.appHasDownloads();
        }
    });
}

oh.app.applist.prototype.appRemoved = function (seq) {
    delete this.list[seq];
    if (this.appListRemovedFunction)
        this.appListRemovedFunction(seq);
}

oh.app.applist.prototype.remove = function (seq,successFunction, errorFunction) {
    this.appProxy.RemoveApp(seq,successFunction,errorFunction);
}


oh.app.applist.prototype.install = function (url, successFunction, errorFunction) {

    var _this = this;
    this.appProxy.InstallAppFromUrl(url, function () {     
        if (successFunction) {
            successFunction();
        }
        _this.appHasDownloads();
    }, errorFunction);
}


/**
* Starts the subscription to receive events 
* The primary property array is changed whenever the device name or device room changes
* @method PrimaryPropertiesArrayChanged
*/
oh.app.applist.prototype.start = function () {
    this.appProxy.subscribe();
}

oh.app.applist.prototype.getAppProxy = function () {
    return this.appProxy;
}

/**
* Class clean up, unsubscribes from node
* @method Dispose
*/
oh.app.applist.prototype.dispose = function () {
    this.appProxy.Unsubscribe();
}






