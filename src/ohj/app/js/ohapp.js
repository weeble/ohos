/**
*
* 
* 
*/

(function ($) {
    // Class
    var ohApp = function (element, options) {
        var elem = $(element);
        var _this = this;
        var alertVisible = false;
        var divdrawer = null;
        var divsplash = null;
        var divlauncher = null;
        var appProxy = null;
        var nodeOnlineTimer = null;
        var settings = $.extend({
            text: 'OPENHOME',
            checkForSystemUpdate: true,
            displayAppManagerLink: true,
            restartWait: 5000,
            restartRetry: 5000,
            localize: false
        }, options || {});

        // Private render
        var render = function () {


            divlauncher = document.createElement('div');
            $(divlauncher).addClass('ohapp');
            $(divlauncher).html(settings.text);
            $(divlauncher).hide();

            elem.append(divlauncher);

            var divicon = document.createElement('div');
            $(divicon).addClass('ohapp-icon');
            $(divlauncher).append(divicon);

            divdrawer = document.createElement('div');
            $(divdrawer).addClass('ohapp-drawer');
            elem.append(divdrawer);
            $(divdrawer).ohdrawer();

            divsplash = document.createElement('div');
            $(divsplash).addClass('ohapp-splash');
            $(divsplash).html('<div class="page-loader" data-controls-ohloader="true"></div>');
            $(elem).append(divsplash);
            $(divsplash).find('.page-loader').ohloader({ loadingtext: '' });
            $('body').css("visibility", 'visible');
            // fade splash screen
            setTimeout(function () { $(divsplash).css("opacity", 0); }, 0);
            // hide splash screen
            setTimeout(function () {

                $(divsplash).hide();
            }, 2000)

            if (!settings.displayAppManagerLink) {
                $(divlauncher).hide();
            }

        };

        // Private render
        var hook = function (url) {
            $('.ohapp', elem).click(function () {
                if (alertVisible) {
                    _this.showSystemUpdate();
                }
                else {
                    window.location = url;
                }
            });
        };
        if (settings.checkForSystemUpdate) {
            var updateProxy = new CpProxyOpenhomeOrgSystemUpdate1(_this.hostUdn);
            updateProxy.State_Changed(function (state) {
                if (state == 'RebootNeeded') {
                    $('body').data('ohapp').showAlert(true);
                }
            });
            updateProxy.subscribe();
        }
        //                setTimeout(function () {
        //                    $('body').data('ohuiapplauncher').showAlert(true);
        //                }, 2000);
        appProxy = new CpProxyOpenhomeOrgApp1(nodeUdn);
        appProxy.GetHostDevice(function (result) {
            _this.hostUdn = result.Udn;
            var appManager = new CpProxyOpenhomeOrgAppManager1(result.Udn);
            appManager.GetPresentationUri(function (result) {
                if (settings.displayAppManagerLink) {
                    $(divlauncher).show();
                    hook(result.AppManagerPresentationUri);
                }
            });

        }, function () { hook(''); });
        render();
      
        if(settings.localize)
        {   
            ohlocal.parse('resources.xml');
        }
        
        if (settings.displayAppManagerLink) {
            $(divlauncher).show();
        }

        this.restart = function (loadingText) {

            setTimeout(function () { $(divsplash).css("opacity", 1); }, 0);
            $(divsplash).show();
            $(divsplash).find('.page-loader').data('ohloader').setText(loadingText);
            setTimeout(function () {
                clearInterval(nodeOnlineTimer);
                nodeOnlineTimer = setInterval(function () {
                    appProxy.GetName(function (result) {
                        if (result && result.Name && result.Name.length > 0) {
                            clearInterval(nodeOnlineTimer);
                            setTimeout(function () {
                                window.location.reload(true);
                            }, settings.restartWait)
                        }
                    });
                }, settings.restartWait);
            }, settings.restartRetry);
        }



        this.showSystemUpdate = function () {
            $(divdrawer).data('ohdrawer').showWarning({
                questionText: 'Your hub has been upgraded!',
                subText: 'For the upgrade to take effect, please restart your hub.',
                backgroundColor: '#5E646C',
                cancelBtnText: 'Not just now',
                okBtnText: 'Restart now!',
                onSuccessFunction: function () {
                    updateProxy.Reboot(function () {
                        _this.showAlert(false);
                        // TODO resource this
                        _this.restart('Restarting your hub, please wait...');
                    });
                }
            });
        };

        this.showAlert = function (show) {
            alertVisible = show;
            if (show) {
                elem.find('.ohapp-icon').addClass('ohapp-icon-alert');
                _this.showSystemUpdate();
            }
            else {
                elem.find('.ohapp-icon').removeClass('ohapp-icon-alert');
            }
        };

    };

    // Plugin
    $.fn.ohapp = function (options) {
        return this.each(function () {
            var element = $(this);
            // TODO - data does not work in zepto, you will need zepto.data.js
            if (element.data('ohapp')) return;
            element.data('ohapp', new ohApp(this, options));
        });
    };

    // Init
    $(document).ready(function () {
        // $('body').ohuiapplauncher();
    });
})(window.jQuery || window.Zepto);
