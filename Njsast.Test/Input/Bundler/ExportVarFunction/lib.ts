function fn() {
  return "ko";
}

export var efn = fn;

efn = function () {
  return "ok";
};
