;(function($) {
    ohjui['ohjlazylist'] = function(element, options) {
        var LOADTEXTFOOTERSPEED = 200;
        var lazyloadInProgress = false,viewPortverticalItems = 0,viewPortHorizontalItems = 0,listitemHeight = 0,resizeThrottle =null, noData = false,loadTextHeight = 0;
        var elem = $(element);
        var _this = this;
        var currentStartIndex = 0;
        var settings = $.extend({
            extend: null,
            onrendersegment: null,
            ongetdata: null,
            threshold: 5,
            overflow: 0.5,
            loadText: 'Loading...',
            showLoadText: 'footer'
        }, options || {});

        // Private Methods
        var refresh = function() {
            // work out how much space is available
            var containerWidth = $.fn.getOuterWidth(elem);
            var containerHeight = $.fn.getOuterHeight(elem);
            var listitemWidth = $.fn.getOuterWidth(elem.find('li').first());
            listitemHeight = $.fn.getOuterHeight(elem.find('li').first());
            viewPortHorizontalItems = Math.max(Math.floor(containerWidth/listitemWidth),1);
            viewPortverticalItems =  Math.max(Math.floor(containerHeight/listitemHeight),1);
            _this.getPageScroller().refreshScroller();
        };

        
        // Private Methods
        var getEndIndex = function() {
            return (viewPortverticalItems*viewPortHorizontalItems)+(Math.floor(viewPortverticalItems*viewPortHorizontalItems*settings.overflow));
        };

        var getNextDataSegment = function() {
            if(!noData) {
                lazyloadInProgress = true;
                showProgress();
                var segmentsize = getEndIndex();
                var endIndex = currentStartIndex+segmentsize;
                var segment = _this.getData(currentStartIndex,endIndex,function(segment) {
                    if(segment.length > 0) {
                        elem.trigger('rendersegment',segment);
                        currentStartIndex = currentStartIndex + segment.length;
                        if(segment.length < segmentsize)
                        {
                            noData = true;
                        }
                    }
                    else {
                        noData = true;
                    }
                    lazyloadInProgress = false;
                    hideProgress();
                }); 
            }
          
        };

        var showProgress = function() {
            if(settings.showLoadText === 'footer') {
                var html = '<div class="ohjlazylist-loader">'+settings.loadText+'</div>';
                elem.append(html);
                var loadText = elem.find('.ohjlazylist-loader');
                loadTextHeight = loadText.height();
                loadText.css({'bottom': '-'+loadTextHeight+'px'});
                loadText.animate({'bottom':'0'},LOADTEXTFOOTERSPEED);
            }
        };

        var hideProgress = function() {
            if(settings.showLoadText) {
                elem.find('.ohjlazylist-loader').animate({'bottom': '-'+loadTextHeight+'px'},LOADTEXTFOOTERSPEED);
                setTimeout(function() {
                    elem.find('.ohjlazylist-loader').remove();
                },LOADTEXTFOOTERSPEED);
            }
        };

        var render = function() {
            elem.initPlugin('ohjlazylist');
            elem.hookPlugin(settings);
            elem.css({'position':'relative'});

            elem.find('ul').html('<li style="float:left;">&nbsp;</li>'); // Dummy to work out height of list item
            elem.find('.ohjpagescroller').on('scroll',function() {
                var overflowheight = elem.find('.content')[0].scrollHeight- $.fn.getOuterHeight(elem);
                if(!lazyloadInProgress && 
                    _this.getPageScroller().getScrollPosition() >= (overflowheight - (settings.threshold * listitemHeight)))
                {                     
                    getNextDataSegment();
                }
            });
            refresh();

            elem.find('ul').html(''); // Remove dummy to calculate space available
            _this.getData(currentStartIndex,getEndIndex(),function(segment) {
                currentStartIndex = segment.length;
                elem.trigger('rendersegment',segment);
            }); 
            // Events
            $(window).bind('resize',function() {
            // TODO multiple binds to page
                clearTimeout(resizeThrottle);
                resizeThrottle = setTimeout(function() {
                    refresh(); 
                },'onorientationchange' in window ? 500 : 0);
            });
        };

        this.getPageScroller = function() {
            return elem.find('.ohjpagescroller').data('ohjpagescroller');
        }
        
        this.destroy = function() {
            elem.destroyPlugin();
        };

        this.getData = function(startIndex, endIndex,onSuccess)
        {
            elem.trigger('getdata',[startIndex,endIndex,onSuccess]);
        }

        if(settings.extend)
            settings.extend.call(this,elem,settings);

        render();
    };
    
    $.fn.createPlugin('ohjlazylist');

})(window.jQuery || window.Zepto);
