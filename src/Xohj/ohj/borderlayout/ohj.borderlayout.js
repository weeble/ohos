/*
* BorderLayout
*/ 

;(function($) {
    ohjui['ohjborderlayout'] = function(element, options) {
            var elem = $(element);
            var _this = this;
            var settings = $.extend({
                extend: null,
                topheight: null,
                bottomheight: null,
                leftwidth: null,
                rightwidth: null,
                height: null
            }, options || {});


            // Private Methods
            var render = function() {
                elem.initPlugin('ohjborderlayout');
                elem.hookPlugin(settings);

                if(settings.height == null) settings.height = $.fn.getOuterHeight(elem);
                elem.css({'height': settings.height });
                if( $.fn.getOuterHeight(elem) > 0)
                {
                    var mainLayout = elem.children('.ohjborderlayout-main');
                    var leftLayout = mainLayout.children('.ohjborderlayout-left');
                    var midLayout = mainLayout.children('.ohjborderlayout-middle');
                    var rightLayout = mainLayout.children('.ohjborderlayout-right');
                    var topLayout = elem.children('.ohjborderlayout-top');
                    var bottomLayout = elem.children('.ohjborderlayout-bottom');
                
                    if(settings.topheight == null) settings.topheight = $.fn.getOuterHeight(topLayout);
                    if(settings.bottomheight == null) settings.bottomheight = $.fn.getOuterHeight(bottomLayout);
                    if(settings.leftwidth == null) settings.leftwidth = $.fn.getOuterWidth(leftLayout);
                    if(settings.rightwidth == null) settings.rightwidth = $.fn.getOuterWidth(rightLayout);
                    

                    topLayout.css({ 'height': settings.topheight || 0 });
                    leftLayout.css({ 'width': settings.leftwidth || 0});
                    bottomLayout.css({ 'height': settings.bottomheight || 0 });
                    mainLayout.css({ 'top' : settings.topheight || 0,
                                      'bottom' : settings.bottomheight || 0 });
                    rightLayout.css({ 'width': settings.rightwidth || 0});
                    midLayout.css({ 'left' : settings.leftwidth || 0 ,
                                    'right' : settings.rightwidth || 0 });
               }
               else
               {
                    elem.hide();
                    throw 'BorderLayout could not determine the height of the container.';
               }
            };
            
            // Add / override extensions to load
            if(settings.extend)
                settings.extend.call(this,elem,settings);
            render();
    };
    
    $.fn.createPlugin('ohjborderlayout');

})(window.jQuery || window.Zepto);
