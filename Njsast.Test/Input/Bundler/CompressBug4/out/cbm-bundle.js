(() => {
    function r() {
        if (this.prop) {
            try {
                something();
            } catch (r) {
                addError(r);
            } finally {
                log("finally");
            }
        }
    }
    r();
})();

