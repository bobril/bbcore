var lib1 = require("./lib1");
var lib2 = require("./lib2");

function method() {
  return lib1.a + lib2.b;
}

exports.method = method;
