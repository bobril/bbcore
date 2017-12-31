export class Base {
    hello() { console.log("Base"); }
}

export class Derived extends Base {
    hello() { console.log("Derived"); }
}
