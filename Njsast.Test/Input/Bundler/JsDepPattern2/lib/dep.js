if (typeof module !== "undefined" && module.exports) {
  var window = window || {};
  var phoenix = phoenix || {};
  phoenix.json = phoenix.json || {};
  module.exports = phoenix.json;
}
(function (p, s) {
  "use strict";
  window.phoenix = window.phoenix || {};
  phoenix.json = phoenix.json || {};
  phoenix.json.X = false;
  p = phoenix.json;
  p.doIt = function () {
    console.log("Ok");
  };
})();
