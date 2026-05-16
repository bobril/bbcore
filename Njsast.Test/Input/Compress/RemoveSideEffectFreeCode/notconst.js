var a = { b: false, c: false };

function fn() {
  a["b"] = true;
}

if (a.b) {
  console.log("Ok");
}

fn();
