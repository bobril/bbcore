function sealed(target: Function) {}
function logged(target: unknown, key: string, descriptor: PropertyDescriptor) {}

@sealed
class Service {
    @logged
    run(): void {}
}
