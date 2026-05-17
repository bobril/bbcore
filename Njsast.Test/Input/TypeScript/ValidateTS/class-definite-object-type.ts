class Bot {
    private buttons!: {
        left: boolean;
        right: boolean;
    }
    private selection!: { start: number; end: number };

    read() {
        return this.selection.start;
    }
}

export const value = new Bot().read();
