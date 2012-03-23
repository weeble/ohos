(function ($) {
    var ohDrawer = function (element, options) {

        var elem = $(element);
        var dialog = null;
        var obj = this;
        var pos = 0;
        var openedPosition = 0;
        var opened = false;
        var closeTimer = null;
        var clickToClose = true;
        var settings = $.extend({
            height: 30
        }, options || {});

        var render = function () {
            elem.addClass('ohdrawer');
            openedPosition = elem.height();

            elem.css({
                'opacity': '1',
                '-webkit-transition-property': '-webkit-transform',
                '-webkit-transition-duration': '400ms',
                'top': '-' + openedPosition + 'px'
            });
        };

        var hook = function () {
            elem.bind('click', function () {
                if (clickToClose)
                    obj.close();
            });
        };

        var setPosition = function (position) {
            pos = position;
            elem.css('-webkit-transform', 'translate3d(0,' + pos + 'px,0)');
            if(pos == 0)
                this.opened = false;
            else
                this.opened = true;
        }


        render();
        hook();

        this.showError = function (msg,onClose) {
           showAutoCloseDrawer(msg,'#FF7777',onClose)
        }

        this.showSuccess = function (msg,onClose) {
           showAutoCloseDrawer(msg,'#54D654',onClose)
        }

        var showAutoCloseDrawer = function (msg,backgroundcolor,onClose) {
            if(closeTimer)
                clearTimeout(closeTimer);
            var timeout = 0;
            if(this.opened)
            {
                timeout = 400;
                setPosition(0);
            }
            setTimeout(function() {
                clickToClose = true;
                elem.html(msg);
                openedPosition = elem.height();
                elem.css({ 'background-color': backgroundcolor,
                    'top': '-' + openedPosition + 'px'
                });
                elem.addClass('ohdrawer-opened');
                setPosition(openedPosition);
               
                closeTimer = setTimeout(function () {
                    if(onClose)
                        onClose();
                    obj.close();
                }, 3000);
         
            },timeout);
        }

        this.showForm = function (options) {
            var opt = $.extend({
                onSuccessFunction: null,
                onCancelFunction: null,
                labelValue: '',
                inputValue: '',
                cancelBtnText: 'Cancel',
                okBtnText: 'OK',
                backgroundColor: '#649BEE'
            }, options || {});
            
            var html =
            '<form>'+
                '<label >'+opt.labelValue+'</label>' +
                '<input class="ohdrawer-input" value="'+opt.inputValue+'"/>'+
                '<div class="clear"></div>'+
                '<div class="action">' +
                    '<button class="ohdrawer-cancel small sm">'+opt.cancelBtnText+'</button>' +
                    '<button class="ohdrawer-ok small sm">'+opt.okBtnText+'</button>'+
                '</div>'+
            '</form>';
            showPrompt(html, opt.onSuccessFunction,opt.onErrorFunction, opt.backgroundColor);
        }

        this.showWarning = function (options) {
            
            var opt = $.extend({
                onSuccessFunction: null,
                onCancelFunction: null,
                questionText: 'Are you sure you wish to delete?',
                cancelBtnText: 'No',
                okBtnText: 'Yes',
                backgroundColor: '#ECC66C',
                subText: '',
                cssClass: '',
            }, options || {});
            
            var html =
            '<div class="ohdrawer-container '+opt.cssClass +'">' +
                '<label class="ohdrawer-questiontext">' + opt.questionText + '</label>' +
                '<div class="ohdrawer-subtext"><label>' + opt.subText + '</label></div>' +
                '<div class="clear"></div>' +
                '<div class="action">' +
                    '<button class="ohdrawer-cancel small sm">'+opt.cancelBtnText+'</button>' +
                    '<button class="ohdrawer-ok small sm">'+opt.okBtnText+'</button>' +
                '</div>' +
            '</div>';
            showPrompt(html, opt.onSuccessFunction,opt.onErrorFunction, opt.backgroundColor);
        }

        var showPrompt = function (html, onSuccessFunction, onErrorFunction, backgroundColor) {
            if(closeTimer)
            {
         
                clearTimeout(closeTimer);
               }
            var timeout = 0;
            
            if(this.opened)
            {
                timeout = 400;
                setPosition(0);
            }
            setTimeout(function() {
                clickToClose = false;
                elem.html(html);
                var close = elem.find('.ohdrawer-cancel');
                if (close) {
                     close.unbind().bind('click', function () {
                        obj.close();
                        if (onErrorFunction)
                            onErrorFunction();
                     
                        return false;
                    });
                }

                var ok = elem.find('.ohdrawer-ok');
                if (ok) {
                    ok.unbind().bind('click', function () {
                        obj.close();
                        if (onSuccessFunction)
                            onSuccessFunction(elem.find('.ohdrawer-input').val());
                        
                        return false;
                    });
                }

                openedPosition = elem.height();
                elem.css({ 'background-color': backgroundColor,
                    'top': '-' + openedPosition + 'px'
                });
                elem.addClass('ohdrawer-opened');
                setPosition(openedPosition);
            },timeout);

        }

        this.close = function () {
            setPosition(0);
            elem.removeClass('ohdrawer-opened');
        }

    };

    $.fn.ohdrawer = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohdrawer'))
                return;
            element.data('ohdrawer', new ohDrawer(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohdrawer]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function			
            var element = $(this);

            if (element.data('ohdrawer'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            // TODO make code initialize read off attributes in markup
            var options = {};

            element.data('ohdrawer', new ohDrawer(this, options));
        });
    });
})(window.jQuery || window.Zepto);
