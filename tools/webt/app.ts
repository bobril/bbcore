import * as b from "bobril";
import * as longPollingClient from "./longPollingClient";

b.selectorStyleDef("body, html", { margin: 0, padding: 0, width: "100%", height: "100%" });

let c = new longPollingClient.Connection("/bb/api/test");

let connected = false;
let testUrl = "";
let iframe: HTMLIFrameElement = null;
let reconnectDelay = 0;

function reconnect() {
    console.log("Connecting");
    c.connect();
    c.send("newClient", {
        userAgent: navigator.userAgent
    });
    b.invalidate();
}

c.onClose = () => {
    console.log("Disconnected");
    connected = false;
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
            console.log("Waiting");
            b.invalidate();
            break;
        }
        case "test": {
            window["specFilter"] = data.specFilter;
            testUrl = data.url;
            console.log("Testing:", data.url, data.specFilter);
            b.invalidate();
            if (iframe != null) document.body.removeChild(iframe);
            iframe = document.createElement("iframe");
            iframe.style.width = "100%";
            iframe.style.height = "100%";
            iframe.style.border = "none";
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

b.init(() => undefined);

window["bbTest"] = (message: string, data: any) => {
    if (message.substr(0, 5) == "whole") {
        console.log(message);
    }
    if (message.substr(0, 9) == "wholeDone") {
        console.log("Testing finished in " + ((data as number) / 1000).toFixed(1) + "s");
    }
    c.send(message, data);
};
