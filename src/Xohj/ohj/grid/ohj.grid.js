;(function($) {
    ohjui['ohjgrid'] =  function(element, options) {
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            rowcount : null,
            columncount : null,
            items : null,
            extend: null
        }, options || {});
            
        // Public Methods
     
        // Private Methods
        var render = function() {
            elem.hookPlugin(settings);
        };

        if(settings.extend)
            settings.extend.call(this,elem,settings);
        render();
    };
    
    $.fn.createPlugin('ohjgrid');

})(window.jQuery || window.Zepto);
