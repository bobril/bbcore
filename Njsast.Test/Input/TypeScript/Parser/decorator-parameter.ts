function param(target: unknown, key: string, index: number) {}

class Service {
    run(@param value: string): void {}
}
