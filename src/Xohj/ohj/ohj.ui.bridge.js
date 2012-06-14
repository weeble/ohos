// Normalize Zepto and jQuery


$.fn.getOuterWidth = function (obj) {
    return (window.jQuery ? obj.outerWidth(false) : obj.width());
}

$.fn.getOuterHeight = function (obj) {
    return (window.jQuery ? obj.outerHeight(false) : obj.height());
}