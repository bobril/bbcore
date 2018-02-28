function liveReloadWaiter() {
    var xhr = new (<any>window).XMLHttpRequest();
    xhr.open("GET", "./bb/api/livereload/##Idx##", true);
    xhr.onabort = () => {
        this.close();
        liveReloadWaiter();
    }
    xhr.onreadystatechange = () => {
        if (xhr.readyState === 4) {
            if (xhr.status < 200 || xhr.status >= 300) {
                this.close();
                liveReloadWaiter();
            } else {
                this.close();
                window.location.reload(true);
                return;
            }
        }
    }
    xhr.send();
}

liveReloadWaiter();
