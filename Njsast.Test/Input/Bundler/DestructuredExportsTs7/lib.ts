function make(name: string) {
    return { is: (x: number) => x > 1, get: () => name.length, set: (v: string) => (name = v) };
}

export const { is, get, set } = make("programList/thumbnailView/move");
export const [first, second] = [10, 20];
