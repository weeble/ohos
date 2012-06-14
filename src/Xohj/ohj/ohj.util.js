;(function($) {
    $.fn.uuid = function() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            var r = Math.random()*16|0, v = c == 'x' ? r : (r&0x3|0x8);
            return v.toString(16);
        });
    }
    
    $.fn.stringToFunction = function(obj) {
        if(obj == null)
            return null;
        return typeof obj == "string" && window[obj] ? window[obj] : obj;
    }

   
})(window.jQuery || window.Zepto)