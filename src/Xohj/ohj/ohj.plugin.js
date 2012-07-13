/*
* Example Plugin
*/ 

;(function($) {
    ohjui['ohjplugin'] = function(element, options) {
            var elem = $(element);
            var _this = this;
            var settings = $.extend({
                extend: null
            }, options || {});
            
            
            // Public Methods / Virtual Methods
            this.doSomething = function() {
                
            };
            
            // Private Methods
            var render = function() {
                elem.initPlugin('ohjplugin');
                elem.hookPlugin(settings);
            };
            
            // Add / override extensions to load
            if(settings.extend)
                settings.extend.call(this,elem,settings);
            render();
    };
    
    $.fn.createPlugin('ohjplugin');

})(window.jQuery || window.Zepto);
