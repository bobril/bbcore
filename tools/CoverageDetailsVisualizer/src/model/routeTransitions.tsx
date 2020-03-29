import * as b from "bobril";

export function goToDir(name: string) {
    return () => {
        b.runTransition(b.createRedirectPush("dir", { splat: name }));
    };
}
export function goToFile(name: string) {
    return () => {
        b.runTransition(b.createRedirectPush("file", { splat: name }));
    };
}
export function goToUp(name: string) {
    if (name == "*") return () => {};
    return () => {
        var slashIdx = name.lastIndexOf("/");
        if (slashIdx < 0) b.runTransition(b.createRedirectPush("rootdir"));
        goToDir(name.substr(0, slashIdx))();
    };
}
