;(function($, browserCapability) {
    $.fn.ohjtoolbar = function(options) {
        var ret = [];
        this.each(function() {
            var element = $(this);
            var data = element.data('ohjtoolbar');
            if(data) {
                return data;
            }
            var settings = $.extend(element.data(), options || {});
            data = new ohjtoolbar(this,settings);
            
            element.data('ohjtoolbar', data);
            ret.push(data);
        });
        return ret.length > 1 ? ret : ret[0];
    };

    var ohjtoolbar = function(element, options) {
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            dock: 'none',
            height: '25px'
        }, options || {});
        // Private Methods   
        var render = function() {
            elem.addClass('ohjtoolbar');
            elem.css({
                'position' : 'fixed',
                'height' : settings.height,
                'width' : '100%',
                'left': '0px',
                'top': '0px'
            });

        };
        
        // Public Methods
       

        // browser polyfills
        //if( typeof (browserCapability) == 'undefined' || !browserCapability.isTouch) {
        //    $.extend(this, new $.fn.ohjpagesliderpointer(element, settings));
        //} else {
        //    $.extend(this, new $.fn.ohjpageslidertouch(element, settings));
        //}

        render();
    };
    
    $().ready(function () {
      
        $('body').find('[data-use-ohjtoolbar]').each(function () {  
            var element = $(this);
            if (element.data('ohjtoolbar'))
                return;
            element.data('ohjtoolbar', new ohjtoolbar(this, element.data()));
        });
    });

})(window.jQuery || window.Zepto, window.browserCapability);
