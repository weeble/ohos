;(function($, browserCapability) {
    $.fn.ohjpageslider = function(options) {
        var ret = [];
        this.each(function() {
            var element = $(this);
            var data = element.data('ohjpageslider');
            if(data) {
                return data;
            }
            var settings = $.extend(element.data(), options || {});
            data = new ohjpageslider(this,settings);
            
            element.data('ohjpageslider', data);
            ret.push(data);
        });
        return ret.length > 1 ? ret : ret[0];
    };

    var ohjpageslider = function(element, options) {
        var elem = $(element), currentPage, history = [], inProgress = false;
        var _this = this;
        var settings = $.extend({
            height : '450px',
            speed : 200,
            onload : null,
            onunload : null
        }, options || {});
        // Private Methods
        var slidePage = function(page, back) {
            if(!inProgress) {
                var pageid = page.attr('id');
                if(page.length > 0 && pageid != null) {
                    if(pageid != currentPage.attr('id')) // Prevent navigation to same page
                    {
                        var onloadFunc = $.fn.stringToFunction(settings.onload);
                        if(onloadFunc!=null) { 
                            onloadFunc.call(_this,currentPage);
                        }
                        var onunloadFunc = $.fn.stringToFunction(settings.onunload);
                        if(onunloadFunc!=null) { 
                            onunloadFunc.call(_this,currentPage);
                        }
                        inProgress = true;
                        //var has3d = ('WebKitCSSMatrix' in window && 'm11' in new WebKitCSSMatrix())
                        currentPage.animate({
                            left : back ? '100%' : '-100%'
                        }, settings.speed);
                        page.show();
                        page.css({
                            left : back ? '-100%' : '100%'
                        });
                        page.animate({
                            left : '0%'
                        }, settings.speed);
                        setTimeout(function() {
                            if(!back)
                                history.push(currentPage.attr('id'));
                            currentPage = page;
                            inProgress = false;
                        }, settings.speed);
                    }
                } else {
                    throw 'Invalid Page';
                }
            }
        }
        
        var render = function() {
            elem.addClass('ohjpageslider');
            elem.css({
                'position' : 'relative',
                'height' : settings.height,
                'width' : '100%',
                'overflow' : 'hidden'
            });

            var pages = elem.children('div');
            pages.addClass('ohjpageslider-page');
            pages.css({
                'display' : 'none',
                'position' : 'absolute',
                'top' : '0px',
                'left' : '0px',
                'height' : '100%',
                'width' : '100%'
            });

            currentPage = pages.first();
            currentPage.show();
        };
        
        // Public Methods
        this.navigateToPage = function(page) {
            slidePage(page);
        };

        this.navigateBack = function() {
            if(!inProgress) {
                var pageBefore = history.pop();
                if(pageBefore != '') // Prevent going back past the start
                {
                    slidePage($('#' + pageBefore), true);
                }
            }
        };
        
        this.navigateNext = function() {
            slidePage(currentPage.next());
        };


        render();
    };
    
    $().ready(function () {
      
        $('body').find('[data-use-ohjpageslider]').each(function () {  
            var element = $(this);
            if (element.data('ohjpageslider'))
                return;
            element.data('ohjpageslider', new ohjpageslider(this, element.data()));
        });
    });

})(window.jQuery || window.Zepto, window.browserCapability);
