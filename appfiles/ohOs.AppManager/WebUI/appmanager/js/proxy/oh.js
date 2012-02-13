/**
*
* OpenHome Javascript Library
* 
*/

(function (global) {
    /**
    * Creates an instance of OpenHome.
    *
    * @constructor
    */
    function oh() {
        /** @private */this.version = '1.0';
    }

    /**
    * Creates a name space
    *
    * @param {string} ns The namespace to create
    */
    oh.prototype.namespace = function (ns) {
        var nsParts = ns.split('.');
        var root = window;

        for (var i = 0; i < nsParts.length; i++) {
            if (typeof root[nsParts[i]] == 'undefined')
                root[nsParts[i]] = new Object();
            root = root[nsParts[i]];
        }
    }

    /**
    * Getter for Version
    *
    * @returns {string} The OpenHome library version
    */
    oh.prototype.getVersion = function () {
        return this.version;
    }

    /**
    * Log Error
    *
    * @params {string} Error message to log
    */
    oh.prototype.logError = function (error) {
        if (typeof window != 'undefined') // [TODO] change to check type of global
            console.log('oh error: ' + error);
    }

    /**
    * Log logRaw
    *
    * @params {Object} log raw object
    */
    oh.prototype.logRaw = function (error) {
        if (typeof window != 'undefined') // [TODO] change to check type of global
            console.log(error);
    }

    /**
    * Log Message
    *
    * @params {string} Message to log
    */
    oh.prototype.log = function (msg) {
        if (typeof window != 'undefined') // [TODO] change to check type of global
            console.log('oh: ' + msg);
    }

    // [TODO] uncomment we have js script loading order
    //if (global.oh) {
    //    throw new Error('oh.js has already been defined');
    //} else {
        global.oh = new oh();
    //}
})(typeof window === 'undefined' ? this : window);
