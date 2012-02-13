(function ($) {
    var ohPageSlider = function (element, options) {

        var FRAMESPERSLIDE = 10;
        var SLIDEINTERVAL = 50;

        var elem = $(element);
        var obj = this;
        var settings = $.extend({
            speed: 500
        }, options || {});

        var currentScrollPosition = 0;
        var scrollInterval = null;
        var pageCount = 0;
        var step = 0;
        var currentPage = 0;

        var setupWidth = function () {
            elem.find('ul').width(elem.width() * pageCount);
            elem.find('li').css({
                'width': elem.width() + 'px'
            });
        }
        var setupSpeed = function () {
            step = elem.width() / (settings.speed / SLIDEINTERVAL);
        }
        var render = function () {

            pageCount = elem.find('li').size();
            elem.css({
                'overflow': 'hidden'
            });

            elem.find('li').css({
                'display': 'inline-block',
                'float': 'left'
            });

            setupWidth();
            setupSpeed();
        };

        var hook = function () {
            $(window).bind('resize', function () {
                setupWidth();
                setupSpeed();
                destroyScrollInterval();
                elem[0].scrollLeft = currentPage * elem.width();
            });
        }

        var destroyScrollInterval = function () {
            clearInterval(scrollInterval);
            scrollInterval = null;
        }
        var scrollTo = function (step) {
            currentScrollPosition = currentScrollPosition + step;
            elem[0].scrollLeft = currentScrollPosition;
        }

        this.scrollToPage = function (page, scrollComplete) {
            currentPage = page;
            var destScrollPosition = page * elem.width();
            if (scrollInterval)
                clearInterval(scrollInterval);

            scrollInterval = setInterval(function () {
                if (currentScrollPosition < destScrollPosition) {
                    scrollTo(step);
                    if (currentScrollPosition >= destScrollPosition) {
                        destroyScrollInterval();
                        if (scrollComplete)
                            scrollComplete();
                    }
                } else if (currentScrollPosition > destScrollPosition) {
                    scrollTo(step * -1);
                    if (currentScrollPosition <= destScrollPosition) {
                        destroyScrollInterval();
                        if (scrollComplete)
                            scrollComplete();
                    }
                } else {
                    destroyScrollInterval();
                    if (scrollComplete)
                        scrollComplete();
                }
            }, SLIDEINTERVAL);
        }

        render();
        hook();
    };

    $.fn.ohpageslider = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohpageslider'))
                return;
            element.data('ohpageslider', new ohPageSlider(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohpageslider]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function			
            var element = $(this);
            if (element.data('ohpageslider'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            // TODO make code initialize read off attributes in markup
            var options = {};
            if (element.data('speed'))
                options.speed = element.data('speed');

            element.data('ohpageslider', new ohPageSlider(this, options));
        });
    });
})(window.jQuery || window.Zepto);
