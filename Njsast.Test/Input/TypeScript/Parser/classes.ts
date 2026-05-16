abstract class Base {
    protected abstract run(value: string): void;
}

class Worker extends Base implements Runnable {
    public readonly name!: string;
    override run(value: string): void {
        console.log(value);
    }
}
