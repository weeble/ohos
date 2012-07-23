;(function($) {
    ohjui['ohjvirtuallist'] = function(element, options) {
            var list = null,listitemHeight = 0,viewPortverticalItems = 0,viewPortHorizontalItems = 0, scrollTimer = null,itemcount = 0, domcacheindex = [], datacache = {}, datacacheindex = [],startIndex = 0 , endIndex = 0;
            var elem = $(element);
            var _this = this;
            var settings = $.extend({
                extend: null,
                ongetitemcount: null,
                onrenderitem: null,
                onrenderpendingitem: null,
                onremoveitem: null,
                ongetdata: null,
                maxdomitems: 50,
                maxdatacache: 1000,
                blocksize: 15
            }, options || {});
           
            // Public Methods / Virtual Methods
            this.getScroller = function() {
                return elem.find('.ohjscroller').data('ohj');
            }

            this.getItemHeight = function() {
                return listitemHeight;
            }

            var addToDomCache = function(index) {
                domcacheindex.push(index);
                if(domcacheindex.length > settings.maxdomitems) {
                    var removeIndex = domcacheindex.shift();
                    if(domcacheindex.indexOf(removeIndex) == -1)
                    {
                        elem.trigger('removeitem',removeIndex);
                    }
                }
            }

             var addToDataCache = function(index,data) {
                if(datacacheindex.indexOf(parseInt(index)) == -1)
                {
                    datacacheindex.push(parseInt(index));
                    datacache[index] = data;
                    if(datacacheindex.length > settings.maxdatacache) {
                        var removeIndex = datacacheindex.shift();
                        delete datacache[removeIndex];
                    }
                }
            }

            this.setSegment = function(segment) {
                    for (var key in segment) {
                        if (segment.hasOwnProperty(key)) {
                            addToDataCache(key, segment[key]);
                            elem.trigger('renderitem',[key,segment[key]]);
                        }
                    }
            };

            var getDataCache = function(index) {
                return datacache[index];
            }

            
            var getDataCacheBlock = function(startIndex , endIndex ) {
                
                // TODO could be better optimized 
                var results = {};
                for (var i = startIndex; i < endIndex; i++) {
                    if(datacache[i] === undefined)
                    {
                        results = null;
                        break;
                    }
                    else {
                        results[i] = datacache[i];
                    }
                }
         
                return results;
            }


            var getNextDataSegment = function() {
                clearTimeout(scrollTimer);
                scrollTimer = setTimeout(function() {
                    
                    var scrollPos = _this.getScroller().getScrollPosition();
                    startIndex = Math.max(Math.ceil(scrollPos/listitemHeight),0);
                    var currentBlock = Math.floor(startIndex/settings.blocksize);
                   
                    var startBlock = Math.max(currentBlock - 1,0);
                    var endBlock = Math.min(currentBlock + 2,itemcount/settings.blocksize);

                    startIndex = startBlock * settings.blocksize;
                    endIndex = endBlock * settings.blocksize;
                 //   var scrollPos = _this.getScroller().getScrollPosition();
                 //   startIndex = Math.max(Math.ceil(scrollPos/listitemHeight) - settings.overflowcount,0);
                  //  endIndex = Math.min(Math.ceil(scrollPos/listitemHeight)+viewPortverticalItems + settings.overflowcount,itemcount);
                   
                    for (var i = startIndex ; i < endIndex; i++) {
                        if(domcacheindex.indexOf(i) == -1) {
                            elem.trigger('renderpendingitem',i);
                        }
                        addToDomCache(i);
                    }

                    getData(startBlock,endBlock,_this.setSegment);
                },200);
            }


            this.setItemCount = function(count) {
                refresh(count);
            }
            
            var refresh = function(count) {
                itemcount = count;
                elem.find('ul').html('<li>&nbsp;</li>');
                var containerWidth = $.fn.getOuterWidth(elem);
                var containerHeight = $.fn.getOuterHeight(elem);
                var listitemWidth = $.fn.getOuterWidth(elem.find('li').first());
                listitemHeight = $.fn.getOuterHeight(elem.find('li').first());
                viewPortHorizontalItems = Math.max(Math.floor(containerWidth/listitemWidth),1);
                viewPortverticalItems =  Math.max(Math.floor(containerHeight/listitemHeight),1);
                elem.find('ul').css({'height':listitemHeight*itemcount+'px'});
                _this.getScroller().refreshScroller();
                elem.find('ul').html(''); // Remove dummy to calculate space available
                getNextDataSegment();
            }
            
            // Private Methods
            var render = function() {
                elem.initPlugin('ohjvirtuallist');
                elem.hookPlugin(settings);
               
                setTimeout(function () {
                    elem.trigger('getitemcount');
                },0);
            //    elem.find('li').css({'height':'1000px'});
            //    _this.getScroller().setOptions({'height':'1000px'});
                elem.find('.ohjscroller').on('scroll',function() {              
                    getNextDataSegment();
                });

                list =  elem.find('.ohjlist').data('ohj');
            };

            var getData = function(sBlock, eBlock,onSuccess)
            {
              //  console.log('blocks '+ sBlock + ' ' + eBlock);
                var diff = eBlock - sBlock;
                var getStartData = null , getEndData = null ;
              //  console.log(diff);
                for(var i = sBlock ; i < sBlock + diff ; i ++)
                {
                    // console.log('blocksize '+i * settings.blocksize + ' ' +  (i+1) * settings.blocksize);
                     var cacheddata = getDataCacheBlock(i * settings.blocksize, (i+1) * settings.blocksize );
    
                     if(cacheddata != null)
                     {
                        onSuccess(cacheddata);
                     }
                     else 
                     {
                        if(getStartData == null) 
                            getStartData = i * settings.blocksize;

                        getEndData = (i+1) * settings.blocksize
                     }
                  //   console.log(cacheddata);
                
                }
                 //  console.log(getStartData+ ' '  + getEndData);
            //    var cacheddata = null ; //getDataCacheBlock(sIndex, eIndex);
            //    if (cacheddata === null) {
                    if(getStartData != null) {
                        elem.trigger('getdata',[getStartData,getEndData,onSuccess]);
                    }
          //      }
          //      else {
            //         onSuccess(cacheddata);
           //     }
            };

            this.destroy = function() {
                var scroller = _this.getScroller();
                if(scroller!=null) { scroller.destroy(); }
                if(list!=null) { list.destroy(); }
                elem.destroyPlugin();
            };

            
            // Add / override extensions to load
            if(settings.extend)
                settings.extend.call(this,elem,settings);
            render();
    };
    
    $.fn.createPlugin('ohjvirtuallist');

})(window.jQuery || window.Zepto);
