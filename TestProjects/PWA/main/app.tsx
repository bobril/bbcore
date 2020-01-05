import * as b from "bobril";
import * as Comlink from "comlink";

if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register(b.asset("project:../sw")).then(function() {
        console.log("Service Worker Registered");
    });
}

var obj = (Comlink.wrap(new Worker(b.asset("project:../worker"))) as unknown) as {
    inc(): Promise<void>;
    readonly counter: Promise<number>;
};

async function test() {
    console.log(`Counter: ${await obj.counter}`);
    await obj.inc();
    console.log(`Counter: ${await obj.counter}`);
}

test();

function App() {
    return <h1>Example PWA app</h1>;
}

b.init(() => <App />);
