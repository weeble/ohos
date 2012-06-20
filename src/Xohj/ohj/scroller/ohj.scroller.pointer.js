
;(function($) {
    ohjui['ohjscrollerpointer'] = function(elem, settings) {
        this.renderScroller = function() {
            elem.addClass('nano');
            elem.css({'height':settings.height});
            var innerDiv = $('<div/>').html(elem.html());
            elem.html(innerDiv);
            elem.children('div').addClass('content');
            elem.nanoScroller();

            elem.find('.content').on('scroll',function() {
                elem.trigger('scroll');
            });
        };
            
        this.refreshScroller = function() {
            elem.nanoScroller();
        };

        this.scrollToTop = function() {
            elem.find('.content')[0].scrollTop = 0;
             elem.nanoScroller({scroll:'top'});
        };

        this.scrollToBottom = function() {
            elem.find('.content')[0].scrollTop =  elem.find('.content')[0].scrollHeight;
            elem.nanoScroller({scroll:'bottom'});
        };

        this.getScrollPosition = function() {
            return elem.find('.content')[0].scrollTop; 
        };

        this.destroy = function() {
            elem.find('.slider').off('mousemove');
            elem.nanoScroller({ stop: true });
            elem.destroyPlugin();
        };
    };
    
    $.fn.ohjscrollerpointer = function(element,settings) {
       return new ohjui.ohjscrollerpointer(element,settings);
    };
})(window.jQuery || window.Zepto);


/** 
 *     
 *  Nano scroller CSS 0.6.4
 *  http://jamesflorentino.com/jquery.nanoscroller/ 
 * 
 * **/

 (function(e,i,j){var l,g,k,m,n,o;n={paneClass:"pane",sliderClass:"slider",sliderMinHeight:20,contentClass:"content",iOSNativeScrolling:!1,preventPageScrolling:!1,disableResize:!1,alwaysVisible:!1};l="Microsoft Internet Explorer"===i.navigator.appName&&/msie 7./i.test(i.navigator.appVersion)&&i.ActiveXObject;g=null;k={};o=function(){var c,a;c=j.createElement("div");a=c.style;a.position="absolute";a.width="100px";a.height="100px";a.overflow="scroll";a.top="-9999px";j.body.appendChild(c);a=c.offsetWidth-
c.clientWidth;j.body.removeChild(c);return a};m=function(){function c(a,b){this.options=b;g||(g=o());this.el=e(a);this.doc=e(j);this.win=e(i);this.generate();this.createEvents();this.addEvents();this.reset()}c.prototype.preventScrolling=function(a,b){"DOMMouseScroll"===a.type?("down"===b&&0<a.originalEvent.detail||"up"===b&&0>a.originalEvent.detail)&&a.preventDefault():"mousewheel"===a.type&&a.originalEvent&&a.originalEvent.wheelDelta&&("down"===b&&0>a.originalEvent.wheelDelta||"up"===b&&0<a.originalEvent.wheelDelta)&&
a.preventDefault()};c.prototype.updateScrollValues=function(){var a;a=this.content[0];this.maxScrollTop=a.scrollHeight-a.clientHeight;this.scrollTop=a.scrollTop;this.maxSliderTop=this.paneOuterHeight-this.sliderHeight;this.sliderTop=this.scrollTop*this.maxSliderTop/this.maxScrollTop};c.prototype.handleKeyPress=function(a){var b;if(38===a||33===a||40===a||34===a)b=100*((38===a||40===a?40:490)/(this.contentHeight-this.paneHeight)),b=b*this.maxSliderTop/100,this.sliderY=38===a||33===a?this.sliderY-b:
this.sliderY+b,this.scroll();else if(36===a||35===a)this.sliderY=36===a?0:this.maxSliderTop,this.scroll()};c.prototype.createEvents=function(){var a=this;this.events={down:function(b){a.isBeingDragged=!0;a.offsetY=b.pageY-a.slider.offset().top;a.pane.addClass("active");a.doc.bind("mousemove",a.events.drag).bind("mouseup",a.events.up);return!1},drag:function(b){a.sliderY=b.pageY-a.el.offset().top-a.offsetY;a.scroll();a.updateScrollValues();a.scrollTop>=a.maxScrollTop?a.el.trigger("scrollend"):0===
a.scrollTop&&a.el.trigger("scrolltop");return!1},up:function(){a.isBeingDragged=!1;a.pane.removeClass("active");a.doc.unbind("mousemove",a.events.drag).unbind("mouseup",a.events.up);return!1},resize:function(){a.reset()},panedown:function(b){a.sliderY=b.offsetY-0.5*a.sliderHeight;a.scroll();a.events.down(b);return!1},scroll:function(b){a.isBeingDragged||(a.updateScrollValues(),a.sliderY=a.sliderTop,a.slider.css({top:a.sliderTop}),null!=b&&(a.scrollTop>=a.maxScrollTop?(a.options.preventPageScrolling&&
a.preventScrolling(b,"down"),a.el.trigger("scrollend")):0===a.scrollTop&&(a.options.preventPageScrolling&&a.preventScrolling(b,"up"),a.el.trigger("scrolltop"))))},wheel:function(b){if(null!=b)return a.sliderY+=-b.wheelDeltaY||-b.delta,a.scroll(),!1},keydown:function(b){var c;if(null!=b&&(c=b.which,38===c||33===c||40===c||34===c||36===c||35===c))a.sliderY=isNaN(a.sliderY)?0:a.sliderY,k[c]=setTimeout(function(){a.handleKeyPress(c)},100),b.preventDefault()},keyup:function(b){null!=b&&(b=b.which,a.handleKeyPress(b),
null!=k[b]&&clearTimeout(k[b]))}}};c.prototype.addEvents=function(){var a;a=this.events;this.options.disableResize||this.win.bind("resize",a.resize);this.slider.bind("mousedown",a.down);this.pane.bind("mousedown",a.panedown).bind("mousewheel",a.wheel).bind("DOMMouseScroll",a.wheel);this.content.bind("mousewheel",a.scroll).bind("DOMMouseScroll",a.scroll).bind("touchmove",a.scroll).bind("keydown",a.keydown).bind("keyup",a.keyup)};c.prototype.removeEvents=function(){var a;a=this.events;this.options.disableResize||
this.win.unbind("resize",a.resize);this.slider.unbind("mousedown",a.down);this.pane.unbind("mousedown",a.panedown).unbind("mousewheel",a.wheel).unbind("DOMMouseScroll",a.wheel);this.content.unbind("mousewheel",a.scroll).unbind("DOMMouseScroll",a.scroll).unbind("touchmove",a.scroll).unbind("keydown",a.keydown).unbind("keyup",a.keyup)};c.prototype.generate=function(){var a,b,c,h,f;c=this.options;h=c.paneClass;f=c.sliderClass;a=c.contentClass;this.el.append('<div class="'+h+'"><div class="'+f+'" /></div>');
this.content=e(this.el.children("."+a)[0]);this.content.attr("tabindex",0);this.slider=this.el.find("."+f);this.pane=this.el.find("."+h);g&&(b={right:-g},this.el.addClass("has-scrollbar"));c.iOSNativeScrolling&&(null==b&&(b={}),b.WebkitOverflowScrolling="touch");null!=b&&this.content.css(b);c.alwaysVisible&&this.pane.css({opacity:1,visibility:"visible"});return this};c.prototype.elementsExist=function(){return this.el.find("."+this.options.paneClass).length};c.prototype.restore=function(){this.stopped=
!1;this.pane.show();return this.addEvents()};c.prototype.reset=function(){var a,b,c,h,f,e,d;this.elementsExist()||this.generate().stop();this.stopped&&this.restore();a=this.content[0];c=a.style;h=c.overflowY;l&&this.content.css({height:this.content.height()});b=a.scrollHeight+g;e=this.pane.outerHeight();d=parseInt(this.pane.css("top"),10);f=parseInt(this.pane.css("bottom"),10);f=e+d+f;d=Math.round(f/b*f);d=d>this.options.sliderMinHeight?d:this.options.sliderMinHeight;"scroll"===h&&"scroll"!==c.overflowX&&
(d+=g);this.maxSliderTop=f-d;this.contentHeight=b;this.paneHeight=e;this.paneOuterHeight=f;this.sliderHeight=d;this.slider.height(d);this.events.scroll();this.pane.show();this.paneOuterHeight>=a.scrollHeight&&"scroll"!==h?this.pane.hide():this.el.height()===a.scrollHeight&&"scroll"===h?this.slider.hide():this.slider.show();return this};c.prototype.scroll=function(){this.sliderY=Math.max(0,this.sliderY);this.sliderY=Math.min(this.maxSliderTop,this.sliderY);this.content.scrollTop(-1*((this.paneHeight-
this.contentHeight+g)*this.sliderY/this.maxSliderTop));this.slider.css({top:this.sliderY});return this};c.prototype.scrollBottom=function(a){this.reset();this.content.scrollTop(this.contentHeight-this.content.height()-a).trigger("mousewheel");return this};c.prototype.scrollTop=function(a){this.reset();this.content.scrollTop(+a).trigger("mousewheel");return this};c.prototype.scrollTo=function(a){this.reset();a=e(a).offset().top;a>this.maxSliderTop&&(a/=this.contentHeight,this.sliderY=a*=this.maxSliderTop,
this.scroll());return this};c.prototype.stop=function(){this.stopped=!0;this.removeEvents();this.pane.hide();return this};return c}();e.fn.nanoScroller=function(c){var a,b,g,h,f,i;null!=c&&(g=c.scrollBottom,f=c.scrollTop,h=c.scrollTo,b=c.scroll,i=c.stop);a=e.extend({},n,c);this.each(function(){var d;if(d=e.data(this,"scrollbar"))e.extend(d.options,c);else{d=new m(this,a);e.data(this,"scrollbar",d)}return g?d.scrollBottom(g):f?d.scrollTop(f):h?d.scrollTo(h):b==="bottom"?d.scrollBottom(0):b==="top"?
d.scrollTop(0):b instanceof e?d.scrollTo(b):i?d.stop():d.reset()})}})(jQuery,window,document);