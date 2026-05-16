export let ddd: (() => void) | undefined = undefined;

export function setDdd(addd: () => void) {
    ddd = addd;
}
