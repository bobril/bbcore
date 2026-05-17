export const check = (
    value: string,
):
    | {
          ok: true;
      }
    | { ok: false; names: string[] } => {
    return value
        ? { ok: true }
        : { ok: false, names: [] };
};
