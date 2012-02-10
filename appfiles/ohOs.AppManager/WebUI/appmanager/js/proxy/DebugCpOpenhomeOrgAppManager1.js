 

/**
* Service Proxy for CpProxyOpenhomeOrgAppManager1
* @module ohnet
* @class AppManager
*/
	
var CpProxyOpenhomeOrgAppManager1 = function(udn){	

	this.url = window.location.protocol + "//" + window.location.host + "/" + udn + "/openhome.org-AppManager-1/control";  // upnp control url
	this.domain = "openhome-org";
	this.type = "AppManager";
	this.version = "1";
	this.serviceName = "openhome.org-AppManager-1";
	this.subscriptionId = "";  // Subscription identifier unique to each Subscription Manager 
	this.udn = udn;   // device name
	
	// Collection of service properties
	this.serviceProperties = {};
	this.serviceProperties["AppHandleArray"] = new ohnet.serviceproperty("AppHandleArray","binary");
	this.serviceProperties["AppSequenceNumberArray"] = new ohnet.serviceproperty("AppSequenceNumberArray","binary");
}



/**
* Subscribes the service to the subscription manager to listen for property change events
* @method Subscribe
* @param {Function} serviceAddedFunction The function that executes once the subscription is successful
*/
CpProxyOpenhomeOrgAppManager1.prototype.subscribe = function (serviceAddedFunction) {
    ohnet.subscriptionmanager.addService(this,serviceAddedFunction);
}


/**
* Unsubscribes the service from the subscription manager to stop listening for property change events
* @method Unsubscribe
*/
CpProxyOpenhomeOrgAppManager1.prototype.unsubscribe = function () {
    ohnet.subscriptionmanager.removeService(this.subscriptionId);
}


	

/**
* Adds a listener to handle "AppHandleArray" property change events
* @method AppHandleArray_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgAppManager1.prototype.AppHandleArray_Changed = function (stateChangedFunction) {
    this.serviceProperties.AppHandleArray.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readBinaryParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "AppSequenceNumberArray" property change events
* @method AppSequenceNumberArray_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgAppManager1.prototype.AppSequenceNumberArray_Changed = function (stateChangedFunction) {
    this.serviceProperties.AppSequenceNumberArray.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readBinaryParameter(state)); 
	});
}


/**
* A service action to GetAppStatus
* @method GetAppStatus
* @param {Int} AppHandle An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.GetAppStatus = function (AppHandle, successFunction, errorFunction) {
    var result = {};
    result["AppListXml"] = debugxml;
    successFunction(result);
}



/**
* A service action to GetAllDownloadsStatus
* @method GetAllDownloadsStatus
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.GetAllDownloadsStatus = function(successFunction, errorFunction){	
    var result = {};
    result["DownloadStatusXml"] = debugprogressxml;
    successFunction(result);
}


/**
* A service action to GetMultipleAppsStatus
* @method GetMultipleAppsStatus
* @param {String} AppHandles An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.GetMultipleAppsStatus = function(AppHandles, successFunction, errorFunction){	
	var request = new ohnet.soaprequest("GetMultipleAppsStatus", this.url, this.domain, this.type, this.version);		
    request.writeBinaryParameter("AppHandles", AppHandles);
    request.send(function(result){
		result["AppListXml"] = ohnet.soaprequest.readStringParameter(result["AppListXml"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to InstallAppFromUrl
* @method InstallAppFromUrl
* @param {String} AppURL An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.InstallAppFromUrl = function(AppURL, successFunction, errorFunction){	

			successFunction();
	
}


/**
* A service action to UpdateApp
* @method UpdateApp
* @param {Int} AppHandle An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.UpdateApp = function(AppHandle, successFunction, errorFunction){	
	var request = new ohnet.soaprequest("UpdateApp", this.url, this.domain, this.type, this.version);		
    request.writeIntParameter("AppHandle", AppHandle);
    request.send(function(result){
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to RemoveApp
* @method RemoveApp
* @param {Int} AppHandle An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.RemoveApp = function (AppHandle, successFunction, errorFunction) {
    if (successFunction) {
        successFunction();
    }
}


/**
* A service action to CancelDownload
* @method CancelDownload
* @param {String} AppURL An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgAppManager1.prototype.CancelDownload = function(AppURL, successFunction, errorFunction){	
	var request = new ohnet.soaprequest("CancelDownload", this.url, this.domain, this.type, this.version);		
    request.writeStringParameter("AppURL", AppURL);
    request.send(function(result){
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}



