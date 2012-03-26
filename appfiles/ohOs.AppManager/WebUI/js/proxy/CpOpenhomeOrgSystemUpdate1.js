 

/**
* Service Proxy for CpProxyOpenhomeOrgSystemUpdate1
* @module ohnet
* @class SystemUpdate
*/
	
var CpProxyOpenhomeOrgSystemUpdate1 = function(udn){	

	this.url = window.location.protocol + "//" + window.location.host + "/" + udn + "/openhome.org-SystemUpdate-1/control";  // upnp control url
	this.domain = "openhome-org";
	this.type = "SystemUpdate";
	this.version = "1";
	this.serviceName = "openhome.org-SystemUpdate-1";
	this.subscriptionId = "";  // Subscription identifier unique to each Subscription Manager 
	this.udn = udn;   // device name
	
	// Collection of service properties
	this.serviceProperties = {};
	this.serviceProperties["State"] = new ohnet.serviceproperty("State","string");
	this.serviceProperties["Progress"] = new ohnet.serviceproperty("Progress","int");
	this.serviceProperties["Server"] = new ohnet.serviceproperty("Server","string");
	this.serviceProperties["Channel"] = new ohnet.serviceproperty("Channel","string");
	this.serviceProperties["LastError"] = new ohnet.serviceproperty("LastError","string");

            
    this.StateAllowedValues = [];
    this.StateAllowedValues.push("Idle");
    this.StateAllowedValues.push("UpdateAvailable");
    this.StateAllowedValues.push("Downloading");
    this.StateAllowedValues.push("UpdateDownloaded");
    this.StateAllowedValues.push("Updating");
    this.StateAllowedValues.push("RebootNeeded");
                        
}



/**
* Subscribes the service to the subscription manager to listen for property change events
* @method Subscribe
* @param {Function} serviceAddedFunction The function that executes once the subscription is successful
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.subscribe = function (serviceAddedFunction) {
    ohnet.subscriptionmanager.addService(this,serviceAddedFunction);
}


/**
* Unsubscribes the service from the subscription manager to stop listening for property change events
* @method Unsubscribe
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.unsubscribe = function () {
    ohnet.subscriptionmanager.removeService(this.subscriptionId);
}


	

/**
* Adds a listener to handle "State" property change events
* @method State_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.State_Changed = function (stateChangedFunction) {
    this.serviceProperties.State.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "Progress" property change events
* @method Progress_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.Progress_Changed = function (stateChangedFunction) {
    this.serviceProperties.Progress.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readIntParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "Server" property change events
* @method Server_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.Server_Changed = function (stateChangedFunction) {
    this.serviceProperties.Server.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "Channel" property change events
* @method Channel_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.Channel_Changed = function (stateChangedFunction) {
    this.serviceProperties.Channel.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "LastError" property change events
* @method LastError_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.LastError_Changed = function (stateChangedFunction) {
    this.serviceProperties.LastError.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}


/**
* A service action to SetSourceInfo
* @method SetSourceInfo
* @param {String} Server An action parameter
* @param {String} Channel An action parameter
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.SetSourceInfo = function(Server, Channel, successFunction, errorFunction){	
	var request = new ohnet.soaprequest("SetSourceInfo", this.url, this.domain, this.type, this.version);		
    request.writeStringParameter("Server", Server);
    request.writeStringParameter("Channel", Channel);
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to CheckForUpdate
* @method CheckForUpdate
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.CheckForUpdate = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("CheckForUpdate", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to DownloadUpdate
* @method DownloadUpdate
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.DownloadUpdate = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("DownloadUpdate", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to DoUpdate
* @method DoUpdate
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.DoUpdate = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("DoUpdate", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to CancelUpdate
* @method CancelUpdate
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.CancelUpdate = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("CancelUpdate", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to Reboot
* @method Reboot
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgSystemUpdate1.prototype.Reboot = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("Reboot", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Status"] = ohnet.soaprequest.readBoolParameter(result["Status"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}



