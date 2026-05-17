export class CustomError extends Error {
    public name = "CustomError";

    constructor(public message: string, public code: number) {
        super(message);
    }
}
