export function fetchJson<T>(url: string): Promise<T> {
    var promise = new Promise<T>((resolve, reject) => {
        var req = new XMLHttpRequest();
        req.overrideMimeType("application/json");
        req.open("GET", url, true);
        req.onload = () => {
            resolve(JSON.parse(req.responseText));
        };
        req.onerror = () => {
            reject(new Error(req.statusText));
        };
        req.send(null);
    });
    return promise;
}
