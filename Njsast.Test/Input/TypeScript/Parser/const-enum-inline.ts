const enum Flags {
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write
}

console.log(Flags.ReadWrite);
