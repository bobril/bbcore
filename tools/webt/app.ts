import * as b from 'bobril';
import * as longPollingClient from './longPollingClient';

let c = new longPollingClient.Connection('/bb/api/test');

let connected = false;
let wait = false;
let disconnected = false;
let testing = false;
let testUrl = "";
let iframe: HTMLIFrameElement = null;
let reconnectDelay = 0;

function reconnect() {
    disconnected = false;
    c.connect();
    c.send("newClient", {
        userAgent: navigator.userAgent
    });
    b.invalidate();
}

c.onClose = () => {
    connected = false;
    disconnected = true;
    b.invalidate();
    if (reconnectDelay < 30000) reconnectDelay += 1000;
    setTimeout(() => {
        reconnect();
    }, reconnectDelay);
};

c.onMessage = (c: longPollingClient.Connection, message: string, data: any) => {
    if (!connected) {
        connected = true;
        reconnectDelay = 0;
        b.invalidate();
    }
    switch (message) {
        case "wait": {
            wait = true;
            b.invalidate();
            break;
        }
        case "test": {
            testing = true;
            window["specFilter"] = data.specFilter;
            testUrl = data.url;
            b.invalidate();
            if (iframe != null) document.body.removeChild(iframe);
            iframe = document.createElement("iframe");
            document.body.appendChild(iframe);
            iframe.src = testUrl;
            break;
        }
        default: {
            console.log("Unknown message: " + message, data);
            break;
        }
    }
};

reconnect();

b.init(() => {
    if (disconnected) {
        return [{ tag: "h2", children: "Disconnected" }, { tag: "p", children: "reload to try to connect again" }];
    }
    if (!connected) {
        return [{ tag: "h2", children: "Connecting" }, { tag: "p", children: "wait ..." }];
    }
    if (wait) {
        return [{ tag: "h2", children: "Waiting" }, { tag: "p", children: "ready to receive commands" }];
    }
    if (testing) {
        return [{ tag: "h2", children: "Testing" }, { tag: "p", children: testUrl }];
    }
});

window["bbTest"] = (message: string, data: any) => c.send(message, data);
