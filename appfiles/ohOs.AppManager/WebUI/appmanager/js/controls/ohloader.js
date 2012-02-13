(function ($) {
    var ohLoader = function (element, options) {

        var elem = $(element);

        var obj = this;
        var type = '';
        var settings = $.extend({
            speed: 250,
            loadingtext: 'Please wait...',
            displayProgress: false
        }, options || {});

        this.renderSpinner = function () {
            if (type != 'spinner') {
                type = 'spinner';
                elem.removeClass('ohprogressbar');
                elem.addClass('ohloader');
                var html = '<div class="circleG-loader">' +
								'<div class="circleG_1 circleG"></div>' +
								'<div class="circleG_2 circleG"></div>' +
								'<div class="circleG_3 circleG"></div>' +
							'</div>' +
                            '<div style="clear:both"></div>' +
                       '<div class="ohloader-text">' + settings.loadingtext + '</div>';
                elem.html(html);
            }
        };

        this.renderProgress = function () {
            if (type != 'progress') {
                type = 'progress';
                elem.removeClass('ohloader');
                elem.addClass('ohprogressbar');
                var html = '<div class="ohprogressbar-progress" style="width: 0%">' +
							'</div>' +
                            '<div style="clear:both"></div>' +
                       '<div class="ohloader-text">' + settings.loadingtext + '</div>';
                elem.html(html);
            }
        };

        this.show = function () {
            elem.find('.ohprogressbar-progress').css({ 'width': '0%' });
            elem.show();
        }

        this.setText = function (text) {
            elem.find('.ohloader-text').html(text);
        }

        this.hide = function () {
            elem.hide();
        }

        this.hideAnimation = function () {
            elem.find('.circleG-loader').css('visibility', 'hidden');
        }

        this.showAnimation = function () {
            elem.find('.circleG-loader').css('visibility', 'visible');
        }

        this.updateProgress = function (percentage) {
            elem.find('.ohprogressbar-progress').animate({
                width: percentage + '%'
            }, {
                duration: settings.speed
            });
        }

        if (settings.displayProgress) {
            this.renderProgress();
        }
        else {
            this.renderSpinner();
        }

    };

    $.fn.ohloader = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohloader'))
                return;
            element.data('ohloader', new ohLoader(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohloader]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function
            var element = $(this);
            if (element.data('ohloader'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            var options = {};
            if (element.data('speed'))
                options.speed = element.data('speed');
            element.data('ohloader', new ohLoader(this, options));
        });
    });
})(window.jQuery || window.Zepto);
