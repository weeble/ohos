;(function($) {
    ohjui['ohjcontentslider'] =  function(element, options) {
            var elem = $(element), currentPage, history = [], inProgress = false;
            var _this = this;
            var settings = $.extend({
                height : '100%',
                speed : 200,
                onpageload : null,
                onpageloadcomplete : null,
                onpageunload : null,
                extend: null
            }, options || {});
            

            // Private Methods
            var slidePage = function(page, back) {
                if(!inProgress) {
                    var pageid = page.attr('id');
                    if(page.length > 0 && pageid != null) {
                        if(pageid != currentPage.attr('id')) // Prevent navigation to same page
                        {
                            $.fn.blockUI();
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

                            finishTransition(currentPage);
                            
                            if(!back)
                                history.push(currentPage.attr('id'));

                            elem.trigger('pageload',page);
                            elem.trigger('pageunload',currentPage);

                            currentPage = page;
                        }
                    } else {
                        throw 'Invalid Page';
                    }
                }
            }
            
            var finishTransition = function(page) { 
                setTimeout(function() {    
                    inProgress = false;
                    $.fn.unblockUI();
                    page.hide();
                    elem.trigger('pageloadcomplete',page);
                }, settings.speed);
            };

            var render = function() {
                elem.initPlugin('ohjcontentslider');
                elem.hookPlugin(settings);
                elem.css({
                    'position' : 'relative',
                    'height' : settings.height,
                    'width' : '100%',
                    'overflow' : 'hidden'
                });
    
                var pages = elem.children('div');
                pages.addClass('ohjcontentslider-page');
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
                elem.trigger('pageload',currentPage);
            };
            
            // Public Methods
            this.navigateToPage = function(page) {
                slidePage(page);
            };

            this.destroy = function() {
                elem.destroyPlugin();
            };
    
            this.navigateBack = function() {
                if(!inProgress) {
                    var pageBefore = history.pop();
                    if(pageBefore != undefined) // Prevent going back past the start
                    {
                        slidePage($('#' + pageBefore), true);
                    }
                }
            };
            
            this.navigateNext = function() {
                slidePage(currentPage.next());
            };

            this.isLastPage = function() {
                var pages = elem.children('div');
                return currentPage.attr('id') === pages.last().attr('id');
            };
    
            if(settings.extend)
                settings.extend.call(this,elem,settings);
            render();
        
    };
    
    $.fn.createPlugin('ohjcontentslider');

})(window.jQuery || window.Zepto);
