function functionUsingEval() {
    eval('return 1');
}
export function longname(parameter: string) {
    return parameter+functionUsingEval();
}
