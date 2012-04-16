(function ($) {
    var ohWizard = function (element, options) {

        var elem = $(element);
        var obj = this;
        var wizardPages = {};
        var history = [];
        var pageNumber = -1;
        var currentWizardPage = '';
        var wizardData = {};
        var footerrow;
        var settings = $.extend({
            closeButtonText: 'Cancel',
            prevButtonText: 'Back',
            nextButtonText: 'Next',
            onCloseButton: null,
            headerText: '',
            headerIconPath: '',
            heightCalc: null
        }, options || {});

        this.setPrev = function (wizardName, prevText, prevFunction) {
            wizardPages[wizardName].prev = prevFunction;
            footerrow.SetLeftButtonText(prevText);
        };

        this.goBack = function () {
            footerrow.SetRightButtonText(settings.nextButtonText);
            if (pageNumber > 0) {
                pageNumber--;
                $('#ohwizard-ohpageslider').data('ohpageslider').scrollToPage(pageNumber);
                currentWizardPage = history[pageNumber];
                if (pageNumber == 0) {
                    footerrow.SetLeftButtonText(settings.closeButtonText);
                }
                else {
                    wizardPages[currentWizardPage].prevButtonText ? footerrow.SetLeftButtonText(wizardPages[currentWizardPage].prevButtonText) : footerrow.SetLeftButtonText(settings.prevButtonText);
                }
            }
            else {
                if (settings.onCloseButton) {
                    settings.onCloseButton.call(this);
                }
            }

            $('#ohwizard-ohscroll').data('ohscroll').refresh();
        };

        var render = function () {
            elem.addClass('ohwizard');
            new oh.ui.layouts.headerrow(elem.attr('id'), elem.attr('id') + '-headerrow', settings.headerText,
            {
                iconPath: settings.headerIconPath
            });
            elem.append('<div id="ohwizard-ohscroll"></div>');

            footerrow = new oh.ui.layouts.footerrow(elem.attr('id'), elem.attr('id') + '-footerrow',
            {
                leftButtonLabel: settings.closeButtonText,
                rightButtonLabel: settings.nextButtonText,
                onLeftButtonClick: function () {
                    if (wizardPages[currentWizardPage].prev) {
                        wizardPages[currentWizardPage].prev.call(wizardData);
                    }
                    else {
                        obj.goBack();
                    }
                },
                onRightButtonClick: function () {
                    if (wizardPages[currentWizardPage].next) {
                        wizardPages[currentWizardPage].next.call(wizardData);
                    }
                }
            });

            $('#ohwizard-ohscroll').ohscroll(
            {
                heightCalc: settings.heightCalc
            });

            $('#ohwizard-ohscroll').data('ohscroll').addItem('<li><div id="ohwizard-ohpageslider"><ul></ul></div></li>');
            $('#ohwizard-ohpageslider').ohpageslider();
        };


        this.reset = function () {
            var numToRemove = history.length;
            for (var i = 0; i < numToRemove; i++) {
                wizardPages[history[i]].dispose.apply(wizardData);
            }
            $('#ohwizard-ohpageslider').children('ul').html('');
            history = [];
            pageNumber = 0;
            currentWizardPage = null;
        };

        this.close = function () {

            if (settings.onCloseButton) {
                settings.onCloseButton.call(this);
            }
            wizardData = null;
            wizardPages = null;
            history = null;
        };

        this.resetToPage = function (wizardName) {
            var html = $('#ohwizard' + wizardName).html();
            this.reset();
            $('#ohwizard-ohpageslider').data('ohpageslider').setupWidth();
            initWizardPage(wizardName, html);
            if (wizardPages[wizardName].show) {
                currentWizardPage = wizardName;
                wizardPages[wizardName].show.call(wizardData);
            }
            $('#ohwizard-ohpageslider').data('ohpageslider').scrollToPage(0);
            footerrow.SetLeftButtonText(settings.closeButtonText);
            wizardPages[wizardName].nextButtonText != null ? footerrow.SetRightButtonText(wizardPages[wizardName].nextButtonText) : footerrow.SetRightButtonText(settings.nextButtonText);
           
            $('#ohwizard-ohscroll').data('ohscroll').refresh();
        };

        this.gotoPage = function (wizardName) {

            var html = $('#ohwizard' + wizardName).html();
            if (history.indexOf(currentWizardPage) != history.length - 1) {
                // Prev has been done
                if (history[pageNumber + 1] != wizardName) { // navigating back to a different page (we need to overwrite existing page and dispose)
                    var numToRemove = history.length - history.indexOf(currentWizardPage);
                    for (var i = 0; i < numToRemove - 1; i++) {
                        $('#ohwizard-ohpageslider').children('ul').children('li').last().remove();
                        wizardPages[history[i + 1]].dispose.apply(wizardData);
                    }
                    history.splice(history.indexOf(currentWizardPage) + 1, numToRemove);
                    $('#ohwizard-ohpageslider').data('ohpageslider').setupWidth();

                    initWizardPage(wizardName, html);
                }
                else {
                    // Welcome back!

                }
            }
            else {
                initWizardPage(wizardName, html);
            }
            pageNumber++;

            $('#ohwizard-ohpageslider').data('ohpageslider').scrollToPage(history.indexOf(wizardName));

         
            if (pageNumber > 0) {
                wizardPages[wizardName].prevButtonText != null ? footerrow.SetLeftButtonText(wizardPages[wizardName].prevButtonText) : footerrow.SetLeftButtonText(settings.prevButtonText);
            }
            wizardPages[wizardName].nextButtonText != null ? footerrow.SetRightButtonText(wizardPages[wizardName].nextButtonText) : footerrow.SetRightButtonText(settings.nextButtonText);
            if (wizardPages[wizardName].show) {
                currentWizardPage = wizardName;
                wizardPages[wizardName].show.call(wizardData);
                $('#ohwizard-ohpageslider').data('ohpageslider').setupWidth();
            }

            $('#ohwizard-ohscroll').data('ohscroll').refresh();

        };

        var initWizardPage = function (wizardName, html) {
            $('#ohwizard-ohpageslider').data('ohpageslider').addItem('<li id="' + wizardName + '">' + html + '</li>');
            history.push(wizardName);
            if (wizardPages[wizardName].init)
                wizardPages[wizardName].init.call(wizardData);

        };

        this.addPage = function (pageoptions) {
            var page = $.extend({
                name: '',
                init: null,
                show: null,
                next: null,
                prev: null,
                dispose: null
            }, pageoptions || {});

            if (page.name) {
                wizardPages[page.name] = page;
            }
        };

        this.finishWizard = function () {
            $('#ohwizard-ohscroll').data('ohscroll').dispose();
            $('#ohwizard-ohpageslider').data('ohpageslider').dispose();
        };

        render();
    };

    $.fn.ohwizard = function (options) {
        return this.each(function () {
            var element = $(this);
            if (element.data('ohwizard'))
                return;
            element.data('ohwizard', new ohWizard(this, options));
        });
    };

    $().ready(function () {
        $('body').find('[data-controls-ohwizard]').each(function () {
            // TODO ensure that initializing a control this way can be accessed by function			
            var element = $(this);
            if (element.data('ohwizard'))
                return;

            // TODO surely .data() should bring back a data collection.  It doesn't maybe zepto will implement this in future'
            // TODO make code initialize read off attributes in markup
            var options = {};
            element.data('ohwizard', new ohPage(this, options));
        });
    });

})(window.jQuery || window.Zepto);
