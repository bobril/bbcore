export class Connection {
    private url: string;
    private id: string;
    private toSend: any[];
    private sendTimer: number;
    private closed: boolean;
    private longPolling: XMLHttpRequest;
    private heartBeatTimer: number;
    private processingBatch = false;

    onMessage: (connection: Connection, message: string, data: any) => void;
    onClose: (connection: Connection) => void;

    constructor(url: string) {
        this.onMessage = null;
        this.onClose = null;
        this.url = url;
        this.toSend = [];
        this.id = "";
        this.sendTimer = -1;
        this.closed = false;
        this.longPolling = null;
        this.heartBeatTimer = -1;
    }

    connect() {
        this.toSend = [];
        this.id = "";
        this.sendTimer = -1;
        this.closed = false;
        this.longPolling = null;
        this.heartBeatTimer = -1;
        this.processingBatch = false;
        this.reSendTimer();
    }

    send(message: string, data: any) {
        this.toSend.push({ m: message, d: data });
        this.reSendTimer();
    }

    close() {
        if (this.closed) return;
        if (this.longPolling) {
            this.longPolling.abort();
            this.longPolling = null;
        }
        this.closed = true;
        this.toSend = [];
        if (this.onClose != null) this.onClose(this);
        this.reSendTimer();
    }

    private reSendTimer() {
        if (this.sendTimer === -1 && (!this.closed || this.id != "")) {
            this.sendTimer = <any>setTimeout(() => {
                this.doSend();
            }, 10);
        }
    }

    private parseResponse(resp: string): boolean {
        var data;
        try {
            data = JSON.parse(resp);
        } catch (err) {
            return false;
        }
        if (typeof data.id !== "string") {
            return false;
        }
        this.id = data.id;
        if (data.close) {
            return false;
        }
        let m = data.m;
        if (Array.isArray(m)) {
            for (let i = 0; i < m.length; i++) {
                try {
                    this.onMessage(this, m[i].m, m[i].d);
                } catch (err) {
                    console.error("onMessage exception ", m[i], err);
                }
            }
        }
        return true;
    }

    private doSend() {
        this.sendTimer = -1;
        if ((this.closed && this.id === "") || this.processingBatch) return;
        var xhr = new (<any>window).XMLHttpRequest();
        xhr.open("POST", this.url, true);
        xhr.onabort = () => {
            this.close();
        };
        xhr.onreadystatechange = () => {
            if (xhr.readyState === 4) {
                this.processingBatch = false;
                if (xhr.status < 200 || xhr.status >= 300) {
                    this.close();
                } else {
                    if (!this.parseResponse(xhr.responseText)) {
                        this.id = "";
                        this.close();
                        return;
                    }
                    if (!this.longPolling) this.startLongPolling();
                    this.startHeartBeat();
                }
                if (this.toSend.length > 0) this.doSend();
            }
        };
        this.processingBatch = true;
        xhr.send(JSON.stringify(this.closed ? { id: this.id, close: true } : { id: this.id, m: this.toSend }));
        if (this.closed) this.id = "";
        this.toSend = [];
    }

    private startLongPolling() {
        if (this.closed || this.id === "") return;
        var xhr = new (<any>window).XMLHttpRequest();
        xhr.open("POST", this.url, true);
        xhr.onreadystatechange = () => {
            if (xhr.readyState === 4) {
                this.longPolling = null;
                if (xhr.status < 200 || xhr.status >= 300) {
                    this.startLongPolling();
                } else {
                    if (!this.parseResponse(xhr.responseText)) {
                        this.id = "";
                        this.close();
                        return;
                    }
                    this.startLongPolling();
                    this.startHeartBeat();
                }
            }
        };
        xhr.send(JSON.stringify({ id: this.id }));
        this.longPolling = xhr;
    }

    private startHeartBeat() {
        if (this.heartBeatTimer != -1) {
            clearTimeout(this.heartBeatTimer);
        }
        this.heartBeatTimer = setTimeout(() => {
            this.heartBeatTimer = -1;
            this.doSend();
        }, 10000);
    }
}
