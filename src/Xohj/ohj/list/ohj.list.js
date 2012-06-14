;(function($) {
    ohjui['ohjlist'] = function(element, options) {
        var elem = $(element);
        var _this = this;
        var settings = $.extend({
            extend: null
        }, options || {});
            
        // Public Methods
        this.addListItem = function(html) {
            elem.append(html);
            refresh();
        };

        // Private Methods
        var refresh = function() {
            var li = elem.children('li');
            li.addClass('clearfix');
            li.children('p').addClass('ellipsis');
            li.children('h3').addClass('ellipsis');
        };
        
        this.destroy = function() {
            elem.destroyPlugin();
        };

        // Private Methods
        var render = function() {
            elem.initPlugin('ohjlist');
            refresh();
        };


    
        if(settings.extend)
            settings.extend.call(this,elem,settings);

        render();
    };
    
    $.fn.createPlugin('ohjlist');

})(window.jQuery || window.Zepto);
