declare var DEBUG: boolean;

// PureFuncs: assert
export function assert(shouldBeTrue: boolean, messageIfFalse?: string) {
  if (DEBUG && !shouldBeTrue) throw Error(messageIfFalse || "assertion failed");
}
