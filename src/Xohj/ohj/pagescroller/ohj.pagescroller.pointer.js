
;(function($) {
    ohjui['ohjpagesliderpointer'] = function(elem, settings) {
        this.renderScroller = function() {
           elem.addClass('nano');
               elem.css({'height':settings.height});
               var innerDiv = $('<div/>').html(elem.html());
               elem.html(innerDiv);
               elem.children('div').addClass('content');
               elem.nanoScroller();
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
            elem.nanoScroller({ stop: true });
            elem.destroyPlugin();
        };
    };
    
    $.fn.ohjpagesliderpointer = function(element,settings) {
       return new ohjui.ohjpagesliderpointer(element,settings);
    };
})(window.jQuery || window.Zepto);


/** 
 *     
 *  Nano scroller CSS 0.5.9.7
 *  http://jamesflorentino.com/jquery.nanoscroller/ 
 * 
 * **/
(function (e, f, g) {
    var h, i, j; i = { paneClass: "pane", sliderClass: "slider", sliderMinHeight: 20, contentClass: "content", iOSNativeScrolling: !1, preventPageScrolling: !1, disableResize: !1 }; j = function () { var d, a; d = g.createElement("div"); a = d.style; a.position = "absolute"; a.width = "100px"; a.height = "100px"; a.overflow = "scroll"; a.top = "-9999px"; g.body.appendChild(d); a = d.offsetWidth - d.clientWidth; g.body.removeChild(d); return a }; h = function () {
        function d(a, b) {
            this.options = b; this.el = e(a); this.doc = e(g); this.win = e(f); this.generate();
            this.createEvents(); this.addEvents(); this.reset()
        } d.prototype.preventScrolling = function (a, b) { switch (a.type) { case "DOMMouseScroll": ("down" === b && 0 < a.originalEvent.detail || "up" === b && 0 > a.originalEvent.detail) && a.preventDefault(); break; case "mousewheel": ("down" === b && 0 > a.originalEvent.wheelDelta || "up" === b && 0 < a.originalEvent.wheelDelta) && a.preventDefault() } }; d.prototype.createEvents = function () {
            var a = this; this.events = { down: function (b) {
                a.isDrag = !0; a.offsetY = b.clientY - a.slider.offset().top; a.pane.addClass("active");
                a.doc.bind("mousemove", a.events.drag).bind("mouseup", a.events.up); return !1
            }, drag: function (b) { a.sliderY = b.clientY - a.el.offset().top - a.offsetY; a.scroll(); return !1 }, up: function () { a.isDrag = !1; a.pane.removeClass("active"); a.doc.unbind("mousemove", a.events.drag).unbind("mouseup", a.events.up); return !1 }, resize: function () { a.reset() }, panedown: function (b) { a.sliderY = b.clientY - a.el.offset().top - 0.5 * a.sliderH; a.scroll(); a.events.down(b) }, scroll: function (b) {
                var c; !0 !== a.isDrag && (c = a.content[0], c = c.scrollTop / (c.scrollHeight -
c.clientHeight) * (a.paneH - a.sliderH), c + a.sliderH === a.paneH ? (a.options.preventPageScrolling && a.preventScrolling(b, "down"), a.el.trigger("scrollend")) : 0 === c && (a.options.preventPageScrolling && a.preventScrolling(b, "up"), a.el.trigger("scrolltop")), a.slider.css({ top: c + "px" }))
            }, wheel: function (b) { a.sliderY += -b.wheelDeltaY || -b.delta; a.scroll(); return !1 } 
            }
        }; d.prototype.addEvents = function () {
            var a, b, c; b = this.events; c = this.pane; a = this.content; this.options.disableResize || this.win.bind("resize", b.resize); this.slider.bind("mousedown",
b.down); c.bind("mousedown", b.panedown); c.bind("mousewheel", b.wheel); c.bind("DOMMouseScroll", b.wheel); a.bind("mousewheel", b.scroll); a.bind("DOMMouseScroll", b.scroll); a.bind("touchmove", b.scroll)
        }; d.prototype.removeEvents = function () {
            var a, b, c; b = this.events; c = this.pane; a = this.content; this.options.disableResize || this.win.unbind("resize", b.resize); this.slider.unbind("mousedown", b.down); c.unbind("mousedown", b.panedown); c.unbind("mousewheel", b.wheel); c.unbind("DOMMouseScroll", b.wheel); a.unbind("mousewheel",
b.scroll); a.unbind("DOMMouseScroll", b.scroll); a.unbind("touchmove", b.scroll)
        }; d.prototype.generate = function () {
            var a; a = this.options; this.el.append('<div class="' + a.paneClass + '"><div class="' + a.sliderClass + '" /></div>'); this.content = e(this.el.children("." + a.contentClass)[0]); this.slider = this.el.find("." + a.sliderClass); this.pane = this.el.find("." + a.paneClass); this.scrollW = j(); a.iOSNativeScrolling ? this.content.css({ right: -this.scrollW + "px", WebkitOverflowScrolling: "touch" }) : this.content.css({ right: -this.scrollW +
"px"
            })
        }; d.prototype.reset = function () {
            var a, b, c, d, e; this.el.find("." + this.options.paneClass).length || (this.generate(), this.stop()); !0 === this.isDead && (this.isDead = !1, this.pane.show(), this.addEvents()); a = this.content[0]; b = a.style; c = b.overflowY; "Microsoft Internet Explorer" === f.navigator.appName && (/msie 7./i.test(f.navigator.appVersion) && f.ActiveXObject) && this.content.css({ height: this.content.height() }); this.contentH = a.scrollHeight + this.scrollW; this.paneH = this.pane.outerHeight(); e = parseInt(this.pane.css("top"),
10); d = parseInt(this.pane.css("bottom"), 10); this.paneOuterH = this.paneH + e + d; this.sliderH = Math.round(this.paneOuterH / this.contentH * this.paneOuterH); this.sliderH = this.sliderH > this.options.sliderMinHeight ? this.sliderH : this.options.sliderMinHeight; "scroll" === c && "scroll" !== b.overflowX && (this.sliderH += this.scrollW); this.scrollH = this.paneOuterH - this.sliderH; this.slider.height(this.sliderH); this.diffH = a.scrollHeight - a.clientHeight; this.pane.show(); this.paneOuterH >= a.scrollHeight && "scroll" !== c ? this.pane.hide() :
this.el.height() === a.scrollHeight && "scroll" === c ? this.slider.hide() : this.slider.show()
        }; d.prototype.scroll = function () { this.sliderY = Math.max(0, this.sliderY); this.sliderY = Math.min(this.scrollH, this.sliderY); this.content.scrollTop(-((this.paneH - this.contentH + this.scrollW) * this.sliderY / this.scrollH)); this.slider.css({ top: this.sliderY }) }; d.prototype.scrollBottom = function (a) {
            var b, c; b = this.diffH; c = this.content[0].scrollTop; this.reset(); c < b && 0 !== c || this.content.scrollTop(this.contentH - this.content.height() -
a).trigger("mousewheel")
        }; d.prototype.scrollTop = function (a) { this.reset(); this.content.scrollTop(+a).trigger("mousewheel") }; d.prototype.scrollTo = function (a) { this.reset(); a = e(a).offset().top; a > this.scrollH && (a /= this.contentH, this.sliderY = a *= this.scrollH, this.scroll()) }; d.prototype.stop = function () { this.isDead = !0; this.removeEvents(); this.pane.hide() }; return d
    } (); e.fn.nanoScroller = function (d) {
        var a; a = e.extend({}, i, d); this.each(function () {
            var b; b = e.data(this, "scrollbar"); b || (b = new h(this, a), e.data(this,
"scrollbar", b)); return a.scrollBottom ? b.scrollBottom(a.scrollBottom) : a.scrollTop ? b.scrollTop(a.scrollTop) : a.scrollTo ? b.scrollTo(a.scrollTo) : "bottom" === a.scroll ? b.scrollBottom(0) : "top" === a.scroll ? b.scrollTop(0) : a.scroll instanceof e ? b.scrollTo(a.scroll) : a.stop ? b.stop() : b.reset()
        })
    } 
})(jQuery, window, document);