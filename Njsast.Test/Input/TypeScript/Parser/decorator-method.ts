function logged(target: unknown, key: string, descriptor: PropertyDescriptor) {}

class Service {
    @logged
    run(value: string): void {
        console.log(value);
    }
}
