;(function($) {
    ohjui['ohjnavbar'] = function(element, options) {
        var SIDEMARGIN = 5;
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            height: null,
            extend: null,
            leftbuttontext: null,
            rightbuttontext: null,
            onleftbuttonpress: null,
            onrightbuttonpress : null,
            title: null
        }, options || {});

        // Public Methods
        this.setTitle = function(text)
        {
            elem.find('h1').text(text);
        };

        this.refresh  = function() {
            var leftButton = elem.find('.ohjnavbar-left'), rightButton = elem.find('.ohjnavbar-right');
            elem.find('.ohjnavbar-center').css({'margin': '0 '+getButtonSize(rightButton)+'px 0 '+getButtonSize(leftButton)+'px'});
        };

        this.destroy = function() {
            elem.destroyPlugin();
        };

        // Private Methods
        var render = function() {
            elem.initPlugin('ohjnavbar');
            
            var html =  '';

            if(settings.leftbuttontext)
                html += '<button class="btn ohjnavbar-left">'+settings.leftbuttontext+'</button>';

                html += '<h1 class="ohjnavbar-center ellipsis">' +
                                elem.text() +
                        '</h1>';

            if(settings.rightbuttontext)
                html += '<button class="btn ohjnavbar-right">'+settings.rightbuttontext+'</button>';

              
            elem.html(html);
            var leftButton = elem.find('.ohjnavbar-left'), rightButton = elem.find('.ohjnavbar-right');
            var centercss = {
                'text-align':'center',
                'display' : 'block',
                'margin' : '0 '+getButtonSize(rightButton)+'px 0 '+getButtonSize(leftButton)+'px'
                // TODO ^^ zepto and jquery not using the same method to work out width including padding
            }
            
             
                var leftcss = {
                'position':'absolute',
                'top': '6px',
                'left': SIDEMARGIN + 'px'
            }

                var rightcss = {
                'position':'absolute',
                'top': '6px',
                'right': SIDEMARGIN + 'px'
            }

            var css = {'position':'relative'};
            if(settings.height)
            {
                css['height'] = settings.height;
                css['line-height'] = settings.height;
            }
            else 
            {
                css['line-height'] = elem.height()+'px';
            }

            elem.css(css);
            if(settings.title)
            {
                _this.setTitle(settings.title);
            }
            elem.find('.ohjnavbar-center').css(centercss);
            leftButton.css(leftcss);
            rightButton.css(rightcss);

            var onleftFunc = $.fn.stringToFunction(settings.onleftbuttonpress);
            if(onleftFunc!=null) { leftButton.press(onleftFunc); }

            var onrightFunc = $.fn.stringToFunction(settings.onrightbuttonpress);
            if(onrightFunc!=null) { rightButton.press(onrightFunc); }
        };
            
        var getButtonSize = function(btn) {
            return $.fn.getOuterWidth(btn) + (SIDEMARGIN *2); // take into the SIDEMARGIN amount for both the left and right side
        };
            
        if(settings.extend)
            settings.extend.call(this,elem,settings);
        render();
    };
   
    
    $.fn.createPlugin('ohjnavbar');

})(window.jQuery || window.Zepto);
