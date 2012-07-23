;(function($) {
    var _that = this;
    ohjui['ohjpage'] = function(element, options) {
        var elem = $(element),pageHeight = 0,resizeThrottle =null,header = null, scroller = null;
        var _this = this;
        var settings = $.extend({
            extend: null
        }, options || {});

        // Public Methods
        this.refreshPage = function() {
            if(elem.height() > 0 )
            {
                pageHeight = elem.height();
                var height = pageHeight - elem.children('header').height() - elem.children('footer').height();
                if(scroller!=null) { scroller.setOptions({'height': height}); }
                if(header!=null) { header.refresh(); }
            }
        };

        this.getScroller = function() {
            return scroller;
        }

        this.destroy = function() {
            if(scroller!=null) { scroller.destroy(); }
            if(header!=null) { header.destroy(); }
            elem.destroyPlugin();
        };

        // Events
        $(window).bind('resize',function() {
        // TODO multiple binds to page
            clearTimeout(resizeThrottle);
            resizeThrottle = setTimeout(function() {
                _this.refreshPage(); 
            },'onorientationchange' in window ? 500 : 0);
        });

        // Private Methods
        var render = function() {
            elem.initPlugin('ohjpage');
            header = elem.children('header').ohjnavbar();
            scroller = elem.children('article').ohjscroller();
            _this.refreshPage();
        };
    
        if(settings.extend)
            settings.extend.call(this,elem,settings);
        render();
    };
    
    $.fn.createPlugin('ohjpage');

})(window.jQuery || window.Zepto);
