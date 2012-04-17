/**
*
* ohtil
* 
*/

/**
* binarySearch algorithm
* @method binarySearch
*/

ohutil = {};
ohutil.binarySearch = function (array,needle, case_insensitive) {

    if (!array.length) return -1;

    var high = array.length - 1;
    var low = 0;
    case_insensitive = (typeof (case_insensitive) !== 'undefined' && case_insensitive) ? true : false;
    needle = (case_insensitive) ? needle.toLowerCase() : needle;

    while (low <= high) {
        mid = parseInt((low + high) / 2)
        element = array[mid];
        if (element > needle) {
            high = mid - 1;
        } else if (element < needle) {
            low = mid + 1;
        } else {
            return mid;
        }
    }

    return -1;
};

ohutil.diff = function (array,arrayToCompare, addedFunction, removedFunction) {
    if (addedFunction) {
        for (var i = arrayToCompare.length - 1; i >= 0; i--) {
            var id = arrayToCompare[i];
            if (ohutil.binarySearch(array,id) < 0) {
                addedFunction(id);
            }
        }
    }

    if (removedFunction) {
        for (var i = array.length - 1; i >= 0; i--) {
            var id = array[i];
            if (ohutil.binarySearch(arrayToCompare,id) < 0) {
                removedFunction(id);
            }
        }
    }
}


ohutil.remove = function (array,from, to) {
    var rest = array.slice((to || from) + 1 || array.length);
    array.length = from < 0 ? array.length + from : from;
    return array.push.apply(array, rest);
}


ohutil.format = function (str) {
    var formatted = str;
    for (var i = 1; i < arguments.length; i++) {
        var regexp = new RegExp('\\{' + i + '\\}', 'gi');
        formatted = formatted.replace(regexp, arguments[i]);
    }
    return formatted;
};

ohutil.hasSeqNumChanged = function(seqNum1, binary, idArray, updatedFunction) {

    var seqNum2 = {};
    var newSeqNum = ohconvert.byteStringToUint32Array(binary);
 
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
    return seqNum2;
}

