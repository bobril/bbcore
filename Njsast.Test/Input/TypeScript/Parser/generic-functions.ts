function first<T>(items: T[]): T {
    return items[0];
}

const value = first<string>(["a"]);
