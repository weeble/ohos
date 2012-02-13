(function($) {
	var ohProgressBar = function(element, options) {

		var elem = $(element);
		var progressElem = null;
		
		var obj = this;
		var settings = $.extend({
			speed: 250
		}, options || {});
		
		var render = function() {
			elem.addClass('ohprogressbar');
			
			progressElem = $(document.createElement('div'));
			progressElem.addClass('ohprogressbar-progress');
			progressElem.css({'width':'0%'});
			elem.append(progressElem);
		};

		this.updateProgress = function(percentage) {
			progressElem.animate({
				width : percentage + '%'
			}, {
				duration : settings.speed
			});
			
			if(percentage == 100)
			{
				progressElem.css({'width':'0%'});
			}
		}
		render();
	};

	$.fn.ohprogressbar = function(options) {
		return this.each(function() {
			var element = $(this);
			if(element.data('ohprogressbar'))
				return;
			element.data('ohprogressbar', new ohProgressBar(this, options));
		});
	};

	$().ready(function() {
		$('body').find('[data-controls-ohprogressbar]').each(function() {
			// TODO ensure that initializing a control this way can be accessed by function
			var element = $(this);
			if(element.data('ohprogressbar'))
				return;

			// TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
			var options = {};
			if(element.data('speed'))
				options.speed = element.data('speed');
			element.data('ohprogressbar', new ohProgressBar(this, options));
		});
	});
})(window.jQuery || window.Zepto);
