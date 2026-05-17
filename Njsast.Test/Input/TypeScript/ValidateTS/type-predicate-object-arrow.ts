const selected = users
    .filter((x): x is { id: string } => x.id !== undefined)
    .map((x) => x.id);
