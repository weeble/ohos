(function ($) {
    var ohScroll = function (element, options) {

        var elem = $(element);

        var obj = this;
        var iscroll, scrollTimer, scrollPosition = 0, ul, resizeTimer;
        var settings = $.extend({
            onPress: null,
            pressDelay: 150,
            vScroll: true,
            heightCalc: null,
            hScroll: false,
            autoResizeRefresh: true,
            resizeDelay: 300
        }, options || {});

        if (elem.data('ohscroll')) {
            elem.data('ohscroll').dispose();
        }
        var render = function () {
            elem.addClass('ohscroll');
            if (settings.hScroll) {
                elem.addClass('ohscroll-hscroll');
            }
            if (settings.vScroll) {
                elem.addClass('ohscroll-vscroll');

            }

            elem.html('<div class="ohscroll-scroller"><ul></ul></div>');
            ul = elem.find('ul');
            iscroll = new iScroll(elem.attr('id'), {
                bounce: true,
                vScrollbar: false,
                hScrollbar: false,
                onBeforeScrollStart: function (e) {
                    var target = e.target;
                    while (target.nodeType != 1) target = target.parentNode;

                    if (target.tagName != 'SELECT' && target.tagName != 'INPUT' && target.tagName != 'TEXTAREA')
                        e.preventDefault();
                },
                hScroll: settings.hScroll,
                vScroll: settings.vScroll
            });

        };

        var hook = function () {
            if (settings.autoResizeRefresh) {
                $(window).bind('resize', function () {
                    clearTimeout(resizeTimer);
                    resizeTimer = setTimeout(function () {
                        obj.refresh();
                    }, settings.resizeDelay);
                });
            }
        };

        this.hasMoved = function () {
            return iscroll.moved;
        }

        // TODO remove this method once scrollablelist has been deprecated 
        this.setRefresh = function (heightCalcFunction) {
            settings.heightCalc = heightCalcFunction;
        }

        this.addItem = function (html) {
            ul.append(html);
            this.refresh();
        }

        this.refresh = function () {
            var width = 0;
            var height = 300;
            ul.children().each(function (i, e) {
                var el = $(e);
                width += el.width();
            });

            if (settings.heightCalc) {
                height = settings.heightCalc();
            }
            if (settings.hScroll) {
                elem.find('.ohscroll-scroller').css({
                    width: width
                });
            }


            elem.css({ height: settings.vScroll ? height : '100%' });

            setTimeout(function () {
                iscroll.refresh();
            }, 0);
        }

        this.clear = function () {
            ul.html('');
        }

        this.dispose = function () {
            if(iscroll)
            iscroll.destroy();
            iscroll = null;
            clearTimeout(scrollTimer);
            scrollTimer = null;
            elem.removeClass('ohscroll ohscroll-hscroll ohscroll-vscroll');
            this.clear();
        }
        render();
        hook();
    };

    $.fn.ohscroll = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohscroll'))
                return;
            element.data('ohscroll', new ohScroll(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohscroll]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function			
            var element = $(this);

            if (element.data('ohscroll'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            // TODO make code initialize read off attributes in markup
            var options = {};

            element.data('ohscroll', new ohDrawer(this, options));
        });
    });
})(window.jQuery || window.Zepto);
