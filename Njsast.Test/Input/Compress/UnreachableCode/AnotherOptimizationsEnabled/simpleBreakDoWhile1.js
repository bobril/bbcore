label: do {
  while (true) {
    func();
    function func() {
      label: do {
        break label;
      } while (false);
    }
  }
} while (false);
