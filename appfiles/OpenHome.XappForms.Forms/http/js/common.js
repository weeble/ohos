
$().ready(function () {

    // First define a Control class. We'll create instances of this for each control
    // on the page. So if there are five buttons on the page, we'll have five control
    // instances. These will wrap our DOM elements and provide handling for incoming
    // messages from the server.

    function Control(id, domElement) {
        this.id = id;
        this.domElement = domElement;
    }


    // ControlManager:
    // Tracks the control classes available for use, and the controls
    // instantiated on this page.
    function ControlManager() {
        this.classes = {};
        this.controls = {};
    }


    ControlManager.prototype = {
        addControl: function (control) {
            this.controls[control.id] = control;
        },
        deliverControlMessage: function (id, message) {
            this.controls[id].receive(message);
        },
        getControl: function (id) {
            return this.controls[id];
        },
        destroyControl: function (id) {
            this.controls[id].destroy();
            delete this.controls[id];
        },
        registerClass: function (name, klass) {
            var thisControlManager = this;
            this.classes[name] = {
                create: function (id) {
                    return new klass(id, thisControlManager);
                }
            };
        },
        createControl: function (name, id) {
            var control = this.classes[name].create(id);
            this.addControl(control);
            return control;
        }
    };

    controlManager = new ControlManager();

    // Event arguments to return from an event
    var eventArgType = {
        'pointer': function (e) {
            console.log(e);
            return {
                'x': e.pageX,
                'y': e.pageY
            };
        },
        'inputval': function (e) {
            return {
                'val': $(e.srcElement).val()
            };
        }
    };






    // Mixins: The following classes are mixins. The idea is that there's no true
    // single-inheritance relationship between "controls that contain other controls",
    // "controls with subscribable events" and so on. So you create your subclass of
    // Control and "mix in" whichever behaviours you want.

    // Control: Controls can receive messages from the server with the 'receive'
    // function. They are then dispatched to a handler function based on the type
    // property.
    function mixinControl(prototype) {
        prototype.receive = function (message) {
            var type = message.type;
            if (typeof type === 'undefined') return console.log('Undefined message type.');
            if (typeof type !== 'string') return console.log('Non-string message type.');
            if (type.slice(0, 3) !== 'xf-') return console.log('Non-XappForms message.');
            var method = this[type];
            if (typeof method === 'undefined') return console.log('Control cannot handle message.');
            return method.call(this, message);
        };
    };


    // SlottedContainer: This is a control that contains some set of named slots,
    // each of which can contain another control.
    // Requires domElement property.
    function mixinSlottedContainer(prototype) {
        prototype['xf-bind-slot'] = function (message) {
            var childControl = this.controlManager.getControl(message.child);
            if (typeof childControl === 'undefined') return console.log('No such child control.');
            var slotId = 'xf-' + this.id + '-' + message.slot;
            var slotElement = this.domElement.find('.' + slotId).andSelf().filter('.' + slotId);
            slotElement.find('>*').detach();
            slotElement.append(childControl.domElement);
            return null;
        };
        return prototype;
    }

    // EventedControl: This is a control that allows the server to subscribe to
    // events raised by its DOM element.
    // Requires domElement property.
    function mixinEventedControl(prototype, eArgType) {
        prototype.getEventArgs = eventArgType[eArgType];
        prototype['xf-subscribe'] = function (message) {
            var _this = this;
            this.domElement.on(
                message['event'],
                function (e) {
                    xapp.tx({
                        'type': 'xf-event',
                        'control': message.control,
                        'event': message['event'],
                        'object': _this.getEventArgs(e)
                    });
                }
            );
        };
        prototype['xf-unsubscribe'] = function (message) {
            this.domElement.off(message['event']);
        };
        return prototype;
    }

    function applyTemplate(name, id) {
        var topId = 'xf-' + id;
        var topElement = $('#' + name).clone().attr('id', topId);
        topElement.find('[data-xfslot]').each(function (i, el) {
            var jElement = $(el);
            jElement.addClass(topId + '-' + jElement.data('xfslot'));
            jElement.removeData('xfslot');
        });

        $.fn.decoratePlugin(name.replace('xf-', 'ohj'), topElement);
        return topElement;
    }


    // GridControl
    // A 2 by 2 grid of controls.
    function GridControl(id, controlManager) {
        this.id = id;
        this.controlManager = controlManager;
        this.domElement = applyTemplate('xf-grid', id);
    }
    mixinControl(GridControl.prototype);
    mixinSlottedContainer(GridControl.prototype);
    controlManager.registerClass("grid", GridControl);

    // ButtonControl
    // A button with some text.
    function ButtonControl(id, controlManager) {
        this.id = id;
        this.controlManager = controlManager;
        this.domElement = applyTemplate('xf-button', id);
    }
    mixinControl(ButtonControl.prototype);
    mixinEventedControl(ButtonControl.prototype, 'pointer');
    ButtonControl.prototype['xf-set-property'] = function (message) {
        if (message.property !== 'text') return;
        this.domElement.text(message.value);
    };
    controlManager.registerClass("button", ButtonControl);

    // TextboxControl
    // A text box
    function TextboxControl(id, controlManager) {
        this.id = id;
        this.controlManager = controlManager;
        this.domElement = applyTemplate('xf-textbox', id);
    }
    mixinControl(TextboxControl.prototype);
    mixinEventedControl(TextboxControl.prototype, 'inputval');
    TextboxControl.prototype['xf-set-property'] = function (message) {
        if (message.property !== 'text') return;
        this.domElement.val(message.value);
    };
    controlManager.registerClass("textbox", TextboxControl);

    // RootControl
    // Used as a parent to the top-level control.
    function RootControl(controlManager) {
        this.id = 0;
        this.controlManager = controlManager;
        this.domElement = $('<div id="xf-0"></div>').appendTo('body');
        this.domElement.addClass('xf-0-root');
    }
    mixinControl(RootControl.prototype);
    mixinSlottedContainer(RootControl.prototype);

    controlManager.addControl(new RootControl(controlManager));


    $('body').on('xappevent', function (event, data) {
        var message = '';
        //console.log('Received:');
        console.log(data);
        switch (data.type) {
            case 'xf-create':
                controlManager.createControl(data['class'], data.control);
                break;
            case 'xf-destroy':
                controlManager.destroyControl(data.control);
                break;
            case 'xf-method':
                var ctrl = $('#xf-' + data.control).data('ohj');
                var args = [];
                for (var d in data.arguments) {
                    if (data.arguments.hasOwnProperty(d))
                        args.push(data.arguments[d]);
                }
                ctrl[data.method].apply(ctrl, args);
                break;
            default:
                if (data.type.slice(0, 3) === 'xf-') {
                    controlManager.deliverControlMessage(data.control, data);
                } else {
                    console.log('Received but could not understand:');
                    console.log(data);
                }
                break;
        }

    });

});
