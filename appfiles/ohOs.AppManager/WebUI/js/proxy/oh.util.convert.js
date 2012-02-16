/**
*
* Datatype convertors
* 
*/

/**
@namespace oh.util.convert
*/
oh.namespace('oh.util.convert');

/**
* Converts a UInt32 to Byte String
* @method uInt32ToByteString
* @param n UInt32
* @returns Byte String
*/
oh.util.convert.uInt32ToByteString = function (n) {
    return String.fromCharCode(n >>> 24, (n >>> 16) & 0xff, (n >>> 8) & 0xff, n & 0xff)
}

/**
* Converts Byte String to UInt32 with offset
* @method ByteStringToUint32Offset
* @param bs Byte String
* @param offset
* @returns UInt32
*/
oh.util.convert.byteStringToUint32Offset = function (bs, offset) {
    var uint32;
    uint32 = this.byteString4ToUint32(bs.slice(offset, offset + 4));
    return uint32;
}


/**
* Converts UInt32 Array to Byte String
* @method uInt32ArrayToByteString
* @param arr The UInt32 Array
* @returns Byte String
*/
oh.util.convert.uInt32ArrayToByteString = function (arr) { return arr.map(this.uInt32ToByteString).join(''); }

/**
* Converts Byte String 4 to UInt32
* @method ByteString4ToUint32
* @param b Byte String (4)
* @returns UInt32
*/
oh.util.convert.byteString4ToUint32 = function (b) {
    var acc = 0;
    for (var i = 0; i != 4; ++i) {
        acc |= b.charCodeAt(i) << (8 * (3 - i));
    }
    return acc;
}

/**
* Converts Byte String to UInt32 UIntArray
* @method ByteStringToUint32Array
* @param bs Byte String
* @returns UInt32 Array
*/
oh.util.convert.byteStringToUint32Array = function (bs) {
    var arr = new Array(bs.length / 4);
    for (var i = 0; i < bs.length; i += 4) {
        arr[i / 4] = this.byteString4ToUint32(bs.slice(i, i + 4));
    }
    return arr;
}


/**
* Converts Byte String to Int32
* @method ByteStringToUint32
* @param b Byte String
* @returns Int32
*/
oh.util.convert.byteStringToInt32 = function (bs) {

    var arr = new Array(bs.length / 4);
    for (var i = 0; i < bs.length; i += 4) {
        arr[i / 4] = this.byteString4ToSignedInt32(bs);
    }
    return arr;
}

/**
* Converts Byte String 4 to Int32
* @method ByteString4ToSignedInt32
* @param b Byte String (4)
* @returns Int32
*/
oh.util.convert.byteString4ToSignedInt32 = function (b) {
    var u = this.byteString4ToInt32(b);
    if (u >= 0x80000000) {
        u = u - 0x100000000;
    }
    return u;
}

/**
* Converts Byte String 4 to Int32
* @method ByteString4ToInt32
* @param b Byte String (4)
* @returns Int32
*/
oh.util.convert.byteString4ToInt32 = function (b) {
    var acc = 0;

    for (var i = 0; i != 4; ++i) {
        acc = acc | b.charCodeAt(i) << (8 * (3 - i));
    }
    this.int32ToByteString(acc);
    return acc;
}

/**
* Converts Int32 to Byte Strirng
* @method Int32ToByteString
* @param n Int32
* @returns Byte String
*/
oh.util.convert.int32ToByteString = function (n) {
    return String.fromCharCode(n >>> 24, (n >>> 16) & 0xff, (n >>> 8) & 0xff, n & 0xff);
}

/**
* Converts Boolean to Byte String
* @method BooleanToByteString
* @param bs Byte String
* @returns Boolean
*/
oh.util.convert.booleanToByteString = function (bs) {
    return String.fromCharCode(bs);
}

/**
* Converts Byte String to Boolean
* @method ByteStringToBoolean
* @param b boolean
* @returns Byte String
*/
oh.util.convert.byteStringToBoolean = function (b) {
    if (b.length == 1) {
        if (b.charCodeAt(0) == 1) {
            return true;
        }
        else
            return false;
    }
    else {
        oh.logError('byteStringToBoolean: Cannot convert to bool');
    }
}


/**
* Converts Byte String to String
* @method ByteStringToString
* @param bs Byte String
* @returns Byte String
*/
oh.util.convert.byteStringToString = function (bs) {

    return bs;
}

/**
* Converts String to Byte String
* @method StringToByteString
* @param bs Byte String
* @returns Byte String
*/
oh.util.convert.stringToByteString = function (bs) {
    return bs;
}


/**
* Converts Byte String to Binary
* @method ByteStringToBinary
* @param bs Byte String
* @returns Byte String
*/
oh.util.convert.syteStringToBinary = function (bs) {
    return bs;
}

/**
* Converts Binary to Byte String
* @method BinaryToByteString
* @param bs Byte String
* @returns Byte String
*/
oh.util.convert.binaryToByteString = function (bs) {
    return bs;
}


