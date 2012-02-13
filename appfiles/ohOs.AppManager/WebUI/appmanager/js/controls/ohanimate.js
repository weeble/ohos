(function($) {
	var ohAnimate = function(element, options) {

		var elem = $(element);

		var obj = this;
		var settings = $.extend({
			speed : '500',
			delay : '200',
			animate : ''
		}, options || {});

		var render = function() {
			elem.addClass('ohanimate');
			var speed = settings.speed + 'ms';
			var delay = settings.delay + 'ms';
			elem.css({
				'-webkit-animation-duration' : speed,
				'-webkit-animation-delay' : delay,
				'-webkit-animation-timing-function' : 'ease',
				'-webkit-animation-fill-mode' : 'both',
				'-moz-animation-duration' : speed,
				'-moz-animation-delay' : delay,
				'-moz-animation-timing-function' : 'ease',
				'-moz-animation-fill-mode' : 'both',
				'-ms-animation-duration' : speed,
				'-ms-animation-delay' : delay,
				'-ms-animation-timing-function' : 'ease',
				'-ms-animation-fill-mode' : 'both',
				'animation-duration' : speed,
				'animation-delay' : delay,
				'animation-timing-function' : 'ease',
				'animation-fill-mode' : 'both'
			});
		};
		this.animate = function(animation) {
			if(animation != '') {
				elem.addClass(animation);
				setTimeout(function() {
					elem.removeClass(animation);
				}, settings.speed);
			}
		}
		render();
		this.animate(settings.animate);
	};

	$.fn.ohanimate = function(options) {
		return this.each(function() {
			var element = $(this);
			if(element.data('ohanimate'))
				return;
				
			element.data('ohanimate', new ohAnimate(this, options));
		});
	};

	$().ready(function() {
		$('body').find('[data-controls-ohanimate]').each(function() {
			// TODO ensure that initializing a control this way can be accessed by function
			var element = $(this);
			if(element.data('ohanimate'))
				return;

			// TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
			var options = {};

			element.data('ohanimate', new ohAnimate(this, options));
		});
	});
})(window.jQuery || window.Zepto);
