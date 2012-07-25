;(function($) {
    var pluginName = 'ohjtextbox';
    ohjui[pluginName] =  function(element, options) {
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            extend: null,
            onclick: null
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
    
    $.fn.createPlugin(pluginName);

})(window.jQuery || window.Zepto);
