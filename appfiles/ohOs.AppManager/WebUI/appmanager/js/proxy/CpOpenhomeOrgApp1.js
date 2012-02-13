 

/**
* Service Proxy for CpProxyOpenhomeOrgApp1
* @module ohnet
* @class App
*/
	
var CpProxyOpenhomeOrgApp1 = function(udn){	

	this.url = window.location.protocol + "//" + window.location.host + "/" + udn + "/openhome.org-App-1/control";  // upnp control url
	this.domain = "openhome-org";
	this.type = "App";
	this.version = "1";
	this.serviceName = "openhome.org-App-1";
	this.subscriptionId = "";  // Subscription identifier unique to each Subscription Manager 
	this.udn = udn;   // device name
	
	// Collection of service properties
	this.serviceProperties = {};
	this.serviceProperties["Name"] = new ohnet.serviceproperty("Name","string");
	this.serviceProperties["IconUri"] = new ohnet.serviceproperty("IconUri","string");
	this.serviceProperties["DescriptionUri"] = new ohnet.serviceproperty("DescriptionUri","string");
}



/**
* Subscribes the service to the subscription manager to listen for property change events
* @method Subscribe
* @param {Function} serviceAddedFunction The function that executes once the subscription is successful
*/
CpProxyOpenhomeOrgApp1.prototype.subscribe = function (serviceAddedFunction) {
    ohnet.subscriptionmanager.addService(this,serviceAddedFunction);
}


/**
* Unsubscribes the service from the subscription manager to stop listening for property change events
* @method Unsubscribe
*/
CpProxyOpenhomeOrgApp1.prototype.unsubscribe = function () {
    ohnet.subscriptionmanager.removeService(this.subscriptionId);
}


	

/**
* Adds a listener to handle "Name" property change events
* @method Name_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgApp1.prototype.Name_Changed = function (stateChangedFunction) {
    this.serviceProperties.Name.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "IconUri" property change events
* @method IconUri_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgApp1.prototype.IconUri_Changed = function (stateChangedFunction) {
    this.serviceProperties.IconUri.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}
	

/**
* Adds a listener to handle "DescriptionUri" property change events
* @method DescriptionUri_Changed
* @param {Function} stateChangedFunction The handler for state changes
*/
CpProxyOpenhomeOrgApp1.prototype.DescriptionUri_Changed = function (stateChangedFunction) {
    this.serviceProperties.DescriptionUri.addListener(function (state) 
	{ 
		stateChangedFunction(ohnet.soaprequest.readStringParameter(state)); 
	});
}


/**
* A service action to GetName
* @method GetName
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgApp1.prototype.GetName = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("GetName", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["Name"] = ohnet.soaprequest.readStringParameter(result["Name"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to GetIconUri
* @method GetIconUri
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgApp1.prototype.GetIconUri = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("GetIconUri", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["IconUri"] = ohnet.soaprequest.readStringParameter(result["IconUri"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}


/**
* A service action to GetDescriptionUri
* @method GetDescriptionUri
* @param {Function} successFunction The function that is executed when the action has completed successfully
* @param {Function} errorFunction The function that is executed when the action has cause an error
*/
CpProxyOpenhomeOrgApp1.prototype.GetDescriptionUri = function(successFunction, errorFunction){	
	var request = new ohnet.soaprequest("GetDescriptionUri", this.url, this.domain, this.type, this.version);		
    request.send(function(result){
		result["DescriptionUri"] = ohnet.soaprequest.readStringParameter(result["DescriptionUri"]);	
	
		if (successFunction){
			successFunction(result);
		}
	}, function(message, transport) {
		if (errorFunction) {errorFunction(message, transport);}
	});
}



