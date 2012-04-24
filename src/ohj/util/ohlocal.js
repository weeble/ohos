/**
*
* Localisation
* 
*/

/**
@namespace ohlocal
*/
ohlocal = {};

var ohlocalresource = {};
ohlocal.parse = function (file) {

    if (window.XMLHttpRequest) {// code for IE7+, Firefox, Chrome, Opera, Safari
        xmlhttp = new XMLHttpRequest();
    }
    else {// code for IE6, IE5
        xmlhttp = new ActiveXObject("Microsoft.XMLHTTP");
    }
    xmlhttp.open("GET", "resources/"+file, false);
    xmlhttp.send();
    xmlDoc = xmlhttp.responseXML;

    var rt = xmlDoc.getElementsByTagName("str");
    for (i = 0 , j = rt.length; i < j; i++) {
        var id = rt[i].attributes;
        ohlocalresource[id.getNamedItem("id").nodeValue] = rt[i].childNodes[0].nodeValue;
    }
};

ohlocal.str = function (key) {

 	if (ohlocalresource != undefined && ohlocalresource[key] != null) {
        return String(ohlocalresource[key]);
    }
    else {
        console.log("ohlocal.str: " +key + " not found");
        return "";
    }
};

