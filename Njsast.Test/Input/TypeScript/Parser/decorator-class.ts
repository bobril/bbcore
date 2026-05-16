function sealed(target: Function) {}

@sealed
class Service {}

console.log(Service);
