(() => {
    function x_index() {
        if (this.prop) {
            try {
                something();
            } catch (error) {
                addError(error);
            } finally {
                log("finally");
            }
        }
    }
    x_index();
})();

