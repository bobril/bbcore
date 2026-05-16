function inc() {
  console.log("Luck");
}

export function exported() {
  console.log("exp");
}

let expr = Math.random() > 0.5 ? "A" : (inc(), "B");

while (true) {
  break;
}

if (Math.random() > 0.5 && Math.random() < 0.5) {
  console.log("combined conditions");
}

console.log(expr);
