(undefined => {
    var updateSessionInterval = 60 * 1e3, start, stop, __export_sessionTimer;
    function setSessionTimer(newTimer) {
        __export_sessionTimer = newTimer;
    }
    start = function() {
        __export_sessionTimer || setSessionTimer(setInterval(function() {}, updateSessionInterval));
    };
    stop = function() {
        if (__export_sessionTimer) {
            clearInterval(__export_sessionTimer);
            setSessionTimer(undefined);
        }
    };
    start();
    console.log("working");
    stop();
})();

