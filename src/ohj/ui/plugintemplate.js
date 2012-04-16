(function ($) {
    var ohPage = function (element, options) {

        var elem = $(element);
        var obj = this;
        var settings = $.extend({
        }, options || {});


        var render = function () {

        };

        var hook = function () {

        };

        render();
        hook();
    };

    $.fn.ohpage = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohpage'))
                return;
            element.data('ohpage', new ohPage(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohpage]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function			
            var element = $(this);
            if (element.data('ohpage'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            // TODO make code initialize read off attributes in markup
            var options = {};
            element.data('ohpage', new ohPage(this, options));
        });
    });
})(window.jQuery || window.Zepto);
