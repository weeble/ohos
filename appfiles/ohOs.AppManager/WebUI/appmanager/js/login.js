var message;
$().ready(function () {
    message = new drawer('drawer');
    $('#drawer').on('click tap', function () {
        message.close();
    });

    $('#login').on('click tap', function () {
        if (validate()) {
            $.ajax({
                type: 'POST',
                url: '/loginService',
                data: '<?xml version="1.0" encoding="UTF-8" ?><login><username>' + $('#username').val() + '</username><password>' + $('#password').val() + '</password></login>',
                dataType: 'text',
                success: function (data) {
                    window.location = data;
                },
                error: function (xhr, type) {
                    message.open('Login failed, please try again');
                }
            });
        }
        return false;
    });
});

function validate() {
    
    if (trim($('#username').val()) === '') {
        message.open('Please enter a username');
        return false;
    }

    if (trim($('#password').val()) === '') {
        message.open('Please enter a password');
        return false;
    }
    return true;
}

function trim(str) {
    return str.replace(/^\s\s*/, '').replace(/\s\s*$/, '');
}


function drawer(el) {
    this.container = document.getElementById(el);

    this.openedPosition = this.container.clientHeight;

    this.container.style.opacity = '1';
    this.container.style.top = '-' + this.openedPosition + 'px';
    this.container.style.webkitTransitionProperty = '-webkit-transform';
    this.container.style.webkitTransitionDuration = '400ms';
}

drawer.prototype = {
    pos: 0,
    opened: false,

    setPosition: function (pos) {
        this.pos = pos;
        this.container.style.webkitTransform = 'translate3d(0,' + pos + 'px,0)';

        if (this.pos == this.openedPosition) {
            this.opened = true;
        } else if (this.pos == 0) {
            this.opened = false;
        }
    },

    open: function (msg) {
        var _this = this;
        this.container.innerHTML = msg;

        this.setPosition(this.openedPosition);
        setTimeout(function () {
            _this.close();
        }, 3000);
    },

    close: function () {
        this.setPosition(0);
    }
}