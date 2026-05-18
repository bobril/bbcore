type Func<T> = () => T;
type Mapped<T> = { [K in keyof T]: Func<T[K]> };

declare function reproduce(options: number): void;
declare function reproduce<T>(options: Mapped<T>): T

reproduce({
  name:   () => { return 123 }
});
