var ohjui = {};
; (function($) {
    $.fn.createPlugin = function(pluginName) {
            $.fn[pluginName] = function(options) {
                var ret = [];
                this.each(function() {
                    var element = $(this);
                    var data = element.data(pluginName);

                    if(data) {
                        return data;
                    }
                    var settings = $.extend(element.data(), options || {});

                    data = new ohjui[pluginName](this,settings);
                    element.data(pluginName, data);
                    ret.push(data);
                });
                return ret.length > 1 ? ret : ret[0];
            };
        
            $().ready(function () {
                $.fn.initPlugins(pluginName,$('body'));
                $.fn.decoratePlugin(pluginName,$('body'));
            });
        }

    
    $.fn.decoratePlugin = function(pluginName, element) {
       element.find('[data-use-'+pluginName+']').each(function () {  
            var element = $(this);
            if (element.data(pluginName))
                return;
            element.data(pluginName, new ohjui[pluginName](this, element.data()));
        });
    }

    $.fn.hookPlugin = function(settings) {
        var _this = this;
        for(var i in settings) {
        
        if(i.indexOf('on') == 0 && i.length > 2 && settings[i] != null)
        {
            var func = settings[i];
            this.on(i.substring(2),function() {
                if(window[func]) {
                     window[func].apply(_this, arguments);
                }
                else 
                {
                    console.log('Function '+ func + ' does not exist yet');
                }
            });
            }   
        }
    }


    $.fn.initPlugins = function(pluginName,element) {
        element.find('[data-use-'+pluginName+']').each(function () {  
            $(this).initPlugin(pluginName);
        });
    }

    $.fn.initPlugin = function(pluginName) {
        this.addClass('ohjui '+pluginName);
        this.attr('ohjui',pluginName);
    }

    $.fn.decoratePlugins = function(element) {
        var elementList = [];

        // Get list of ohj elements
        element.find('.ohjui').each(function() {
            var _this = $(this);
            elementList.push({ element: _this.attr('id'), depth: _this.parents().length});
        });
        
        // Sort by DOM depth
        elementList.sort(function(a, b) {
            if (a.depth < b.depth) return 1;
            if (a.depth > b.depth) return -1;
            return 0;
        });

        // Decorate in order of lowest level to the root  
        for(i in elementList) {
            var element = elementList[i].element;
            var plugin = element.attr('ohjui');
            if(!element.hasClass(plugin)) {
                if (element.data(plugin))
                    return;
                element.data(plugin, new ohjui[plugin](element, element.data()));
            }
       }
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