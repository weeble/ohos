;(function($) {
    ohj = {
        ohjgrid : function(element, options) {
            var elem = $(element);
            var _this = this;
            var settings = $.extend({
                rowcount : null,
                columncount : null,
                items : null,
                extend: null
            }, options || {});
            
            // Public Methods
     
            // Private Methods
            var render = function() {
                elem.addClass('ohjgrid');
                 
                var tableDiv = '<table>';
                for(var r = 0 ; r < settings.rowcount; r++)
                {
                    tableDiv += '<tr>';
                    for(var c = 0 ; c < settings.columncount; c++)
                    {
                        tableDiv += '<td>';
                        
                        tableDiv += '</td>';       
                    }
                    tableDiv += '</tr>';
                }
                tableDiv += '</table>';
                
                $(tableDiv).appendTo(elem);
            };

            if(settings.extend)
                settings.extend.call(this,elem,settings);
            render();
        }
    };
    
    $.fn.createPlugin('ohjgrid',ohj, true);

})(window.jQuery || window.Zepto);
