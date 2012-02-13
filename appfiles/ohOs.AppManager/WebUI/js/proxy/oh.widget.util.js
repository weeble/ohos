/**
*
* Represents a Preset
* 
*/

/**
@namespace oh.widget.util
*/
oh.namespace('oh.widget.util');

oh.widget.util.hasSeqNumChanged = function(seqNum1, binary, idArray, updatedFunction) {


    var seqNum2 = {};
    try{
    var newSeqNum = oh.util.convert.byteStringToUint32Array(binary);
 
    for (var i = 0; i < newSeqNum.length; i++) {
        seqNum2[idArray[i]] = newSeqNum[i];
    }

    for (var i in seqNum1) {
        var seqNum = seqNum2[i];
        if (seqNum != null) {
            var currentSeqNum = seqNum1[i];
            if (seqNum != currentSeqNum) {
                if(updatedFunction)
                    updatedFunction(i);
            }
        }
    }
    }
    catch(e)
    {
        oh.logError('hasSeqNumChanged: ' + e.message);
    }
    return seqNum2;
}


oh.widget.util.stripId = function(fullid)
{
    try
        {
            var split = String(fullid).split('_');
            if (split.length > 1) {
                return split[split.length-1];
            }
            else
            {
                return fullid;
            }
     }
    catch(e)
    {
        oh.logError('stripId: ' + e.message);
    }
}

// [TODO] in the right place?
oh.widget.util.setDefaultValue = function (object, defaultValue) {
    try {
        return (typeof object == 'undefined') ? defaultValue : object;
    }
    catch (e) {
        oh.logError('setDefaultValue: ' + e.message);
    }
}

// [TODO] in the right place?
oh.widget.util.setDefaultStrValue = function (object, defaultValue) {
    try {
        if (!defaultValue)
            defaultValue = '';
        return (typeof object == 'undefined') || (object == '') ? defaultValue : object;
    }
    catch (e) {
        oh.logError('setDefaultValue: ' + e.message);
    }
}


oh.widget.util.getValueFromBase64Data = function (dataValue, dataType) {
    try {
        dataValue = oh.util.dataformat.fromBase64(dataValue);
        return oh.widget.util.getValueFromData(dataValue,dataType);
    }
    catch (e) {
        oh.logError('oh.widget.util.getValueFromBase64Data: ' + e.message);
    }
}

oh.widget.util.getValueFromData = function (dataValue, dataType) {
    try {
        var value;
        switch (dataType) {
            case 'integer':
                {
                    value = oh.util.convert.byteStringToInt32(dataValue);
                    break;
                }
            case 'string':
                {
                    value = oh.util.convert.byteStringToString(dataValue);
                    break;
                }
            case 'boolean':
                {
                    value = oh.util.convert.byteStringToBoolean(dataValue);
                    break;
                }
            case 'binary':
                {
                    value = OhNet.SoapRequest.byteStringToBinary(value);
                    break;
                }
            case 'color':
                {
                    // [TODO] change to color data type
                    value = oh.util.convert.byteStringToInt32(value);
                    break;
                }
            default:
                oh.logError('oh.widget.util getValueFromData: Property type does not exist: ' + dataType);
        }
        return value;

    }
    catch (e) {
        oh.logError('oh.widget.util.getValueFromData: ' + e.message);
    }
}


oh.widget.util.getDataFromValue = function (value, dataType) {
    try {
        var dataValue;
    
        switch (dataType) {
            case 'integer':
                {
                    dataValue = oh.util.convert.int32ToByteString(value);
                    break;
                }
            case 'string':
                {
                    // TODO - change this 
                    dataValue = OhNet.SoapRequest.readStringParameter(value);
                    break;
                }
            case 'boolean':
                {
                    dataValue = oh.util.convert.booleanToByteString(value);
                    break;
                }
            case 'binary':
                {
                    dataValue = OhNet.SoapRequest.readBinaryParameter(value);
                    break;
                }
            case 'color':
                {
                    // [TODO] change to color data type
                    dataValue =  oh.util.convert.int32ToByteString(value);
                    break;
                }
            default:
                oh.logError('oh.widget.util getDataFromValue: Property type does not exist: ' + dataType);


        }
        return dataValue;

    }
    catch (e) {
        oh.logError('oh.widget.util.getDataFromValue: ' + e.message);
    }
}

oh.widget.util.ensureArray = function (obj) {
    try {
        if (obj) {
            if (!obj.length) {
                var tempObj = obj;
                obj = [];
                obj.push(tempObj);
                return obj;
            }
            else {
                return obj;
            }
        }
        else {
            obj = [];
            return obj;
        }
    }
    catch (e) {
        oh.logError('oh.widget.util.ensureArray: ' + e.message);
    }
}


oh.widget.util.getWidgetProperties = function (xml) {
    var obj = oh.util.dataformat.xmlStringToJson(xml);
    return oh.widget.util.ensureArray(obj.widget.property);
}

oh.widget.util.parsePropertyDetails = function(xml) {
// TODO use xml2json parser
    if (window.DOMParser) {
        parser = new DOMParser();
        xmlDoc = parser.parseFromString(xml, "text/xml");
    }
    else // Internet Explorer
    {
        xmlDoc = new ActiveXObject("Microsoft.XMLDOM");
        xmlDoc.async = "false";
        xmlDoc.loadXML(xml);
    }

    var propertyDetails = new Object();
    var avl = xmlDoc.getElementsByTagName("allowedValue");
    if (avl.length > 0) {
        propertyDetails.allowedValueList = [];
        for (var i = 0; i < avl.length; i++) {
            propertyDetails.allowedValueList.push(avl[i].childNodes[0].nodeValue);
        }
    }
    var avr = xmlDoc.getElementsByTagName("allowedValueRange");
    propertyDetails.allowedValueRange = {};
    if (avr.length > 0) {

        var list = avr[0];

        var max = list.getElementsByTagName("maximum");
        if (max.length > 0)
            propertyDetails.allowedValueRange.maximum = max[0].childNodes[0].nodeValue;
        else
            propertyDetails.allowedValueRange.maximum = 2147483647;
        var min = list.getElementsByTagName("minimum");
        if (min.length > 0)
            propertyDetails.allowedValueRange.minimum = min[0].childNodes[0].nodeValue;
        else
            propertyDetails.allowedValueRange.minimum = -2147483648;
        var step = list.getElementsByTagName("step");
        if (step.length > 0)
            propertyDetails.allowedValueRange.step = step[0].childNodes[0].nodeValue;
        else
            propertyDetails.allowedValueRange.step = 1;
    }
    else {
        propertyDetails.allowedValueRange.maximum = 2147483647;
        propertyDetails.allowedValueRange.minimum = -2147483648;
        propertyDetails.allowedValueRange.step = 1;
    }

    var hint = xmlDoc.getElementsByTagName("hint");
    if (hint.length > 0) {
        propertyDetails.hint = hint[0].childNodes[0].nodeValue;
    }

    return propertyDetails;

}


oh.widget.util.getErrorCode = function (transport) {
    return transport.responseXML.getElementsByTagName("errorCode")[0].childNodes[0].nodeValue;
}

oh.widget.util.getErrorDescription = function (transport) {
    return transport.responseXML.getElementsByTagName("errorDescription")[0].childNodes[0].nodeValue;
}


oh.widget.util.parseWidgetIdFromErrorDescription = function (message) {
    var split = message.split(' ');
    return split[split.length - 1];
}