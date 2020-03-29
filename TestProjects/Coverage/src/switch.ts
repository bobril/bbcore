export function calcSwitch(a: number, b: number) {
  switch (a + b) {
    case 1:
      return 1;
    case 2:
    case 3:
      return 2;
    default:
      return 3;
  }
}
