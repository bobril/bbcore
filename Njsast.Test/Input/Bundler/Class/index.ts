class Base
{
    static Id = "A";

    render() {
        return "Hello";
    }
}

class Deriv extends Base
{
    static Id = "B";

    render() {
        return super.render();
    }
}
