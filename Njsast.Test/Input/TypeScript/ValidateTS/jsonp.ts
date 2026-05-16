export function jsonp(url: string): Promise<void> {
    return new Promise((r, e) => {
        let script = document.createElement("script");
        script.type = "text/javascript";
        script.onload = () => {
            r();
        };
        script.onerror = (_ev) => {
            e("Failed to load " + url);
        };
        script.src = url;
        document.head!.appendChild(script);
    });
}
