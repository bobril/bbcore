module.exports = {
  doIt: function(p) {
    console.log(p);
  },
  dontIt: function() {
    var window = "KO";
    global.console.log(window);
  }
};
