/**
*
* Data Format utilities
* 
*/

/**
@namespace oh.util.dataformat
*/
oh.namespace('oh.util.dataformat');

oh.util.dataformat.parseXML = function (xmlStr) {
    var dom = null;
    if (window.DOMParser) {
        try {
            dom = (new DOMParser()).parseFromString(xmlStr, 'text/xml');
        }
        catch (e) {
            oh.logError('parseXML DOMParser: ' + e.message);
            dom = null;
        }
    }
    else if (window.ActiveXObject) {
        try {
            dom = new ActiveXObject('Microsoft.XMLDOM');
            dom.async = false;
            if (!dom.loadXML(xmlStr)) // parse error ..
                window.alert(dom.parseError.reason + dom.parseError.srcText);
        }
        catch (e) {
            oh.logError('parseXML ActiveXObject: ' + e.message);
            dom = null;
        }
    }
    else {
        oh.logError('parseXML : No xml parser available: ' + xmlStr);
    }
    return dom;
}

oh.util.dataformat.xmlStringToJson = function (xmlStr) {
    try {
        return oh.util.dataformat.xmlToJson(oh.util.dataformat.parseXML(xmlStr));
    }
    catch (e) {
        oh.logError('xmlStringToJson: ' + e.message);
    }
}

oh.util.dataformat.xmlToJson = function (xml) {
    try {

        var json = xmlJsonClass.xml2json(xml,'  ');
        return JSON.parse(json);
    }
    catch (e) {
        oh.logError('xmlToJson: ' + e.message + ' ' + xml);
    }
}

oh.util.dataformat.jsonStringToXml = function (jsonStr) {
    try {
        return oh.util.dataformat.jsonToXml(JSON.parse(jsonStr));
    }
    catch (e) {
        oh.logError('jsonStringToXml: ' + e.message);
    }
}

oh.util.dataformat.jsonToXml = function (json) {
    try {
        return xmlJsonClass.json2xml(json);
    }
    catch (e) {
        oh.logError('jsonToXml: ' + e.message);
    }
}


oh.util.dataformat.toBase64 = function (value) {
    try {
        return btoa(value);
    }
    catch (e) {
        oh.logError('toBase64: ' + e.message);
    }
}

oh.util.dataformat.fromBase64 = function (value) {
    try {
        return atob(value);
    }
    catch (e) {
        oh.logError('toBase64: ' + e.message);
    }
}