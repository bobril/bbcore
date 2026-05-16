// PureFuncs: pure, second
var counter = 0;

function pure() {
  return counter++;
}

function impure() {
  console.log("sideeffect");
  return 42;
}

function second() {
  console.log("trust me this is pure");
}
