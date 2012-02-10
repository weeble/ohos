(function($) {
	var ohModal = function(element, options) {
		
		var elem = $(element);
		var dialog = null;
		var backdrop = null;
		var obj = this;
		var settings = $.extend({
			dialog: ''
		}, options || {});
		
		var render = function() {
	 		backdrop = $(document.createElement('div'));
	 		backdrop.css({
	 			'background-color':'#000',
	 			'position':'fixed',
	 			'top':'0',
	 			'bottom':'0',
	 			'left':'0',
	 			'right':'0',
	 			'z-index':'10000',
	 			'opacity':'0.7'
	 		});
	 		backdrop.hide();
	 		
	 		dialog = $('#'+settings.dialog);
	 		
	 		dialog.css({

	 			'position': 'absolute',
				'top': '50%',
				'left':'50%',

	 			'z-index':'10001'
	 		});
	 		dialog.hide();
			
	 		$('body').append(backdrop);
	 		$('body').append(dialog);
	 		
	 		setupDialogPosition();
		};
		
		
		var setupDialogPosition = function() {
			dialog.css({
	 			'margin-top': '-'+elem.height()/2+'px',
				'margin-left' : '-'+elem.parent().width()/2+'px',
				'width':elem.parent().width()+'px',
			});
		}
		
		var hook = function() {
			elem.click(function(){
				obj.show();
			});	
			
			$(window).bind('resize',function(){
				setupDialogPosition();
			});	
			
			backdrop.click(function() {
				obj.hide();
			});
			
			dialog.find('[data-ohmodal-close]').click(function() {
				obj.hide();
			});
		}
		render();
		hook();
		
		this.show = function() {
			backdrop.show();
			dialog.show();
		}
		
		this.hide = function() {
			backdrop.hide();
			dialog.hide();
		}
	};

	$.fn.ohmodal = function(options) {
		return this.each(function() {
			var element = $(this);
			if(element.data('ohmodal'))
				return;
			element.data('ohmodal', new ohModal(this, options));
		});
	};
	
	$().ready(function() {
		$('body').find('[data-controls-ohmodal]').each(function() {
			// TODO ensure that initializing a control this way can be accessed by function			
			var element = $(this);
			
			if(element.data('ohmodal'))
				return;
			
			// TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
			// TODO make code initialize read off attributes in markup
			var options = {};
			if(element.data('dialog'))
			{
				options.dialog = element.data('dialog');
			}
			element.data('ohmodal', new ohModal(this, options ));
		});
	});
})(window.jQuery || window.Zepto);
