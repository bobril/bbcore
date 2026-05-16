module.exports = function(param) {
  Object.keys(param).forEach(function(name) {
    var orig = param[name];
    param[name] = function(p) {
      orig(name + ":" + p);
    };
  });
  return param;
};
