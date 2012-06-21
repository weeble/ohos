;(function () {
    var istouch = 'ontouchstart' in window;
    var ismobile = false;
    if (screen.width <= '1000' && screen.height <= '1000') { ismobile = true; }
    if (!istouch) { window.document.cookie = 'xappbrowser=desktop'; }
    else if (ismobile) { window.document.cookie = 'xappbrowser=mobile'; }
    else { window.document.cookie = 'xappbrowser=tablet'; }
    window.location.reload(true);
})();
