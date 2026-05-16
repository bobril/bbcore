function logged(value: unknown, context: ClassAccessorDecoratorContext) {}

class Service {
    @logged
    accessor name = "ready";
}
