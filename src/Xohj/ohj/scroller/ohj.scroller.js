;(function($) {
    ohjui['ohjscroller'] = function(element, options) {
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            height:'450px',
            extend: null
        }, options || {});

        // Private Methods   
        var render = function() {
            elem.initPlugin('ohjscroller');
            elem.css({'height':settings.height});
          
            _this.renderScroller();
        };
            
            
        // Public Methods
        this.setOptions = function(options) {
            settings = $.extend(settings, options || {});
            if(options.height)
            {  
                elem.css({'height':options.height});
                _this.refreshScroller();
            }
        };
            
        // Virtual Methods
        this.renderScroller = function() { };
            
        this.refreshScroller = function() { };

        this.scrollToTop = function() { };

        this.scrollToBottom = function() { };

        this.getScrollPosition = function() { };

        this.destroy = function() { };

        if(settings.extend)
            settings.extend.call(this,elem,settings);
        else {
            if('ontouchstart' in window && window.Zepto)
            {
                $.extend(this, new $.fn.ohjscrollertouch(elem, settings));
            }
            else {
                $.extend(this, new $.fn.ohjscrollerpointer(elem, settings));
            }
        }
            
        render();
    };
    
    $.fn.createPlugin('ohjscroller');
    
})(window.jQuery || window.Zepto);

