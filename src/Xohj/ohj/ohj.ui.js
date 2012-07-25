var ohjui = {};
; (function($) {
    $.fn.createPlugin = function(pluginName) {
            $.fn[pluginName] = function(options) {
                var ret = [];
                this.each(function() {
                    var element = $(this);
                    $.extend(element.data(), options || {});
                    ret.push($.fn.decoratePlugin(pluginName,element));
                });
                return ret.length > 1 ? ret : ret[0];
            };
        
            //$().ready(function () {
              //  $.fn.decoratePluginType(pluginName,$('body'));
            //});
        }

    $.fn.hookPlugin = function(settings) {
        var _this = this;
        for(var i in settings) {
            if(i.indexOf('on') == 0 && i.length > 2 && settings[i] != null)
            {
                _this.hookEvent(i.substring(2),settings[i]);
            }   
        }
    }

    $.fn.hookEvent = function(event,func) {
        var _this = this;
        this.on(event,function() {
            if(func!=null && typeof func == "string") {
                if(window[func]) {
                    window[func].apply(_this, arguments);
                }
                else
                {
                    console.log('Function '+ func + ' does not exist yet');
                }
            }
            else if(func!=null && $.isFunction(func))
            {
                func.apply(_this, arguments);
            }
        });
    }

    $.fn.decorateContainerPlugins = function(element) {
        var elementList = [];
        var _this = $(this);
        if(element.attr('data-ohj'))
        {
            elementList.push({ pluginName: _this.attr('data-ohj'), element:_this, depth: 0});
        }
        // Get list of child ohj elements
        element.find('[data-ohj]').each(function() {
            var that = $(this);
            elementList.push({ pluginName: that.attr('data-ohj'), element:that, depth: that.parents().length});
        });
        
        // Sort by DOM depth
        elementList.sort(function(a, b) {
            if (a.depth < b.depth) return 1;
            if (a.depth > b.depth) return -1;
            return 0;
        });

        // Decorate in order of lowest level to the root  
        for(i in elementList) {
            $.fn.decoratePlugin(elementList[i].pluginName,elementList[i].element);
        }
    }

    $.fn.decoratePluginType = function(pluginName, element) {
       element.find('[data-ohj="'+pluginName+'"]').each(function () {  
            $.fn.decoratePlugin(pluginName,$(this));
        });
    }

    $.fn.decoratePlugin = function(pluginName, element) {
        if (element.data('ohjtype'))
            return element.data('ohj');
        element.data('ohjtype',pluginName);
        element.data('ohj', new ohjui[pluginName](element, element.data()));
        element.addClass(pluginName);
        return element.data('ohj');
    }


    $.fn.press = function(onPress) {
        if('ontouchstart' in window && window.Zepto)
        {
            $(this).tap(onPress);
        }
        else 
        {
            $(this).on('click',onPress);
        }
    }

     $.fn.destroyPlugin = function() {
        var data = this.data();
        for(d in data)
        {
            if(d.indexOf('on') == 0 && d.length > 2 && element.data(d) != null)
            {
                element.off(d.substring(2));
            }
            this.removeData(d);
        }
        this.remove();
    }


    $.fn.blockUI = function() {
        if($('#ohjuiblock').length === 0) {
            var mask = $("<div id='ohjuiblock' class='ohjuiblock' style='z-index:9999;width:100%;height:100%;position:absolute;top:0;left:0;'></div>");
            mask.on('touchstart touchmove',function(e) { 
                e.preventDefault();
            });
            $('body').prepend(mask);
        }
    };
    
    $.fn.unblockUI = function() {
        var mask = $('#ohjuiblock');
        if(mask.length > 0) {
            mask.off('touchstart touchmove');
            mask.remove();
        }
    };

    // Based on Simple JavaScript Templating by
    // John Resig - http://ejohn.org/ - MIT Licensed 
    // and Rick Strahl http://www.west-wind.com/weblog/posts/2008/Oct/13/Client-Templating-with-jQuery
    $.fn.templatecache = {};
    $.fn.template = function(str, data) {
        var err = "";
        try {
             var func = $.fn.templatecache[str];
             
            if (!func) {
                var strFunc =
                "var p=[],print=function(){p.push.apply(p,arguments);};" +
                            "with(obj){p.push('" +

                str.replace(/[\r\t\n]/g, " ")
                   .replace(/'(?=[^#]*#>)/g, "\t")
                   .split("'").join("\\'")
                   .split("\t").join("'")
                   .replace(/<#=(.+?)#>/g, "',$1,'")
                   .split("<#").join("');")
                   .split("#>").join("p.push('")
                   + "');}return p.join('');";

                func = new Function("obj", strFunc);
                $.fn.templatecache[str] = func;
            }
            return func(data);
        } catch (e) { err = e.message; }
    return "< # ERROR: " + err.htmlEncode() + " # >";
};
    
})(window.jQuery || window.Zepto)