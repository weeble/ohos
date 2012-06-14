var DELAY = 1000;
var EXPECTEDTOTAL = 0;

var webpage = require('webpage');
var tabs = [];

var results = {
    runtime: 0,
    total: 0 ,
    passed : 0,
    failed : 0
};

// A class that executes a callback once all tasks have been completed
var syncHelper = function (syncTotalCount, onComplete) {
    this.onComplete = onComplete;
    this.syncTotalCount = syncTotalCount;
    this.syncCount = 0;
}

syncHelper.prototype.completeTask = function () {
    this.syncCount++;
    if (this.syncCount === this.syncTotalCount && this.onComplete != null) {
        this.onComplete();
    }
}

// A class that executes a function after a task has been completed
var queueHelper = function () {
    this.queue = [];
    this.currentPosition = 0;
}

queueHelper.prototype.addTask = function (callback) {
    this.queue.push(callback);
}

queueHelper.prototype.completeTask = function () {
    var func = this.queue[this.currentPosition];
    if (func) {
        this.currentPosition++;
        func();   
    }
}

/********************************************
* Setup
********************************************/

var setupSync = new syncHelper(6, startTests);
createPage(function (session) {
    setupSync.completeTask();
    createTab(session, function () {
        setupSync.completeTask();
    });
    createTab(session, function () {
        setupSync.completeTask();
    });
});
createPage(function (session) {
    setupSync.completeTask();
    createTab(session, function () {
        setupSync.completeTask();
    });
    createTab(session, function () {
        setupSync.completeTask();
    });
});

var testQueue = new queueHelper();
/*********************************
* TESTS
*********************************/
function startTests() {
    console.log('Running Tests...');
    testQueue.addTask(testAllCanReceiveMessageEvent);
    testQueue.addTask(testAllReceiveBulkMessages);
    testQueue.addTask(testAllCanReceiveMessageEventAfterDroppedTabs);
    testQueue.addTask(getResults);
    testQueue.completeTask(); // start things off
    
}

/*********************************
* testAllCanReceiveMessageEvent
*********************************/
function testAllCanReceiveMessageEvent() {
    var sent = [];
    var messageQueue = new queueHelper();
    for (var i = 0; i < 10; i++) {
        addMessage(sent, messageQueue, i);
    }

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length; t++) {
            testMessages(t, sent);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length; t++) {
            clearMessages(t);
        }
        messageQueue.completeTask();
    });
   
    messageQueue.addTask(function () {
        testQueue.completeTask();
    });
    messageQueue.completeTask();
}

function addMessage(sent,messageQueue, i) {
    messageQueue.addTask(function () {
        send(0, i.toString());
        sent.push(i);
        setTimeout(function () {
            messageQueue.completeTask();
        }, 100);
    });
}

/*********************************
* testAllReceiveBulkMessages
*********************************/
function testAllReceiveBulkMessages() {
    var MESSAGECOUNT = 5;
    var messageQueue = new queueHelper();

        sendImmediateGroup(0, MESSAGECOUNT * 1);
        sendImmediateGroup(0, MESSAGECOUNT * 2);
        sendImmediateGroup(1, MESSAGECOUNT * 3);
        sendImmediateGroup(1, MESSAGECOUNT * 4);
        sendImmediateGroup(2, MESSAGECOUNT * 5);
        sendImmediateGroup(2, MESSAGECOUNT * 6);
        sendImmediateGroup(3, MESSAGECOUNT * 7);
        sendImmediateGroup(3, MESSAGECOUNT * 8);
        sendImmediateGroup(4, MESSAGECOUNT * 9);
        sendImmediateGroup(4, MESSAGECOUNT * 10);
        sendImmediateGroup(5, MESSAGECOUNT * 11);
        sendImmediateGroup(5, MESSAGECOUNT * 12);
   
    console.log('Expected Total : ' + EXPECTEDTOTAL);
    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length; t++) {
            testCount(t, EXPECTEDTOTAL);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length; t++) {
            clearMessages(t);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length; t++) {
            clearMessages(t);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        testQueue.completeTask();
    });
   
    setTimeout(function () {
        messageQueue.completeTask();
    }, 5000);
}


/*********************************
* testAllCanReceiveMessageEventAfterDroppedTabs
*********************************/
function testAllCanReceiveMessageEventAfterDroppedTabs() {
    var sent = [];
    var messageQueue = new queueHelper();

    messageQueue.addTask(function () {
        tabs[4].evaluate(function () {
            stop();
        });
        tabs[5].evaluate(function () {
            stop();
        });
        console.log('Wait for tabs to disappear from the server...');
        setTimeout(function () {
            messageQueue.completeTask();
        }, 25000);

    });

    for (var i = 0; i < 10; i++) {
        addMessage(sent, messageQueue, i);
    }

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length-2; t++) {
            testMessages(t, sent);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        for (var t = 0; t < tabs.length - 2; t++) {
            clearMessages(t);
        }
        messageQueue.completeTask();
    });

    messageQueue.addTask(function () {
        testQueue.completeTask();
    });
    messageQueue.completeTask();
}



// A set of test helpers

function send(i, value) {
    var page = tabs[i];
    evaluate(page, function (value) {
        sendMessage(value);
    }, value);
}

function stop(i) {
    var page = tabs[i];
    page.evaluate(function () {
        stop();
    });
}

function sendImmediate(i, value) {
    var page = tabs[i];
    evaluate(page, function (value) {
        sendMessage(value);
    }, value);
}


function testMessage(i, value, timeout) {
    setTimeout(function () {
        var page = tabs[i];
        evaluate(page, function (value) {
            testLastMessage(value);
        }, value);
    }, timeout);
}

function clearMessages(i) {
        var page = tabs[i];
        page.evaluate(function () {
            clearMessages();
        });
}


function clearMessage(i, timeout) {
    var page = tabs[i];
    page.evaluate(function () {
        clearMessage();
    });
}

function sendImmediateGroup(tab, count) {
    EXPECTEDTOTAL += count;
    for (var i = 0; i < count; i++) {
        sendImmediate(tab, i.toString());
    }
}

function testMessages(i, array) {
    var page = tabs[i];
    evaluate(page, function (array) {
        testArrayMessage(array);
    }, array);
}

function testCount(i, count) {
    var page = tabs[i];
        evaluate(page, function (count) {
            testCountMessage(count);
        }, count);
}



// Writes the results to console and returns an exit code to console
function getResults() {
    for (i in tabs) {
        var output = tabs[i].evaluate(function () {
            return JSON.stringify(window.qunitDone);
        });
        var jsonoutput = JSON.parse(output);
        results.total += jsonoutput.total;
        results.passed += jsonoutput.passed;
        results.failed += jsonoutput.failed;
        results.runtime = jsonoutput.runtime > results.runtime ? jsonoutput.runtime : results.runtime;
    }
    console.log('Took ' + results.runtime + 'ms to run ' + results.total + ' tests. ' + results.passed + ' passed, ' + results.failed + ' failed.');
    phantom.exit(results.failed > 0 ? 1 : 0);
}


// Creates a new browser instance with one tab
function createPage(onHasSession) {
    var pg = webpage.create();
    pg.onInitialized = function () {
        pg.evaluate(addLogging);
    };
    pg.open('http://127.0.0.1:12921/test/tablet.html');
    pg.onLoadFinished = function (status) {
        if (status === 'success') {
            setTimeout(function () {
                pg.sessionid = pg.evaluate(function () {
                    return xapp.sessionid;
                });
                if (onHasSession)
                    onHasSession(pg.sessionid);
            }, 500);
        }
    };

    pg.onConsoleMessage = function (msg) {
        console.log(msg);
    }
    tabs.push(pg);
};

// Creates a tab for a given session id
function createTab(session,onComplete) {
    var pg = webpage.create();
    pg.onInitialized = function () {
        pg.evaluate(addLogging);
    };
    pg.open('http://127.0.0.1:12921/test/temp.html', function () {
        evaluate(pg, function (session) {
            document.cookie = "XappSession=" + session + "; expires=Thu, 2 Aug 2022 20:47:11 UTC; path=/";
        }, session);
        pg.open('http://127.0.0.1:12921/test/tablet.html', function () { if (onComplete) { onComplete(); } });
    });

    pg.onConsoleMessage = function (msg) {
        console.log(msg);
    }
    tabs.push(pg);
};

// Allows parameters to be passed to the phantomjs evaluate function
function evaluate(pag, func) {
    var args = [].slice.call(arguments, 2);
    var str = 'function() { return (' + func.toString() + ')(';
    for (var i = 0, l = args.length; i < l; i++) {
        var arg = args[i];
        if (/object|string/.test(typeof arg)) {
            str += 'JSON.parse(' + JSON.stringify(JSON.stringify(arg)) + '),';
        } else {
            str += arg + ',';
        }
    }
    str = str.replace(/,$/, '); }');
    return pag.evaluate(str);
};

// Adds qunit logging
function addLogging() {
    window.document.addEventListener("DOMContentLoaded", function () {
        var current_test_assertions = [];
        var module;

        QUnit.moduleStart(function (context) {
            module = context.name;
        });

        QUnit.testDone(function (result) {
            var name = module + ': ' + result.name;
            var i;

            if (result.failed) {
                console.log('Assertion Failed: ' + name);

                for (i = 0; i < current_test_assertions.length; i++) {
                    console.log('    ' + current_test_assertions[i]);
                }
            }
            console.log('Testing of ' + result.name + ' has passed');
            current_test_assertions = [];
        });

        QUnit.log(function (details) {
            var response;

            if (details.result) {
                return;
            }

            response = details.message || '';

            if (typeof details.expected !== 'undefined') {
                if (response) {
                    response += ', ';
                }

                response += 'expected: ' + details.expected + ', but was: ' + details.actual;
            }

            current_test_assertions.push('Failed assertion: ' + response);
        });

        QUnit.done(function (result) {
            //console.log('Took ' + result.runtime + 'ms to run ' + result.total + ' tests. ' + result.passed + ' passed, ' + result.failed + ' failed.');
            window.qunitDone = result;
        });
    }, false);
}
