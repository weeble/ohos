/**
*
* Extensions to existing js objects
* 
*/

/**
* binarySearch algorithm
* @method binarySearch
*/
Array.prototype.binarySearch = function (needle, case_insensitive) {

    if (!this.length) return -1;

    var high = this.length - 1;
    var low = 0;
    case_insensitive = (typeof (case_insensitive) !== 'undefined' && case_insensitive) ? true : false;
    needle = (case_insensitive) ? needle.toLowerCase() : needle;

    while (low <= high) {
        mid = parseInt((low + high) / 2)
        element = this[mid];
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

Array.prototype.diff = function (arrayToCompare, addedFunction, removedFunction) {
    if (addedFunction) {
        for (var i = arrayToCompare.length - 1; i >= 0; i--) {
            var id = arrayToCompare[i];
            if (this.binarySearch(id) < 0) {
                addedFunction(id);
            }
        }
    }

    if (removedFunction) {
        for (var i = this.length - 1; i >= 0; i--) {
            var id = this[i];
            if (arrayToCompare.binarySearch(id) < 0) {
                removedFunction(id);
            }
        }
    }

}


Array.prototype.remove = function (from, to) {
    var rest = this.slice((to || from) + 1 || this.length);
    this.length = from < 0 ? this.length + from : from;
    return this.push.apply(this, rest);
}


String.prototype.format = function () {
    var formatted = this;
    for (var i = 0; i < arguments.length; i++) {
        var regexp = new RegExp('\\{' + i + '\\}', 'gi');
        formatted = formatted.replace(regexp, arguments[i]);
    }
    return formatted;
};
