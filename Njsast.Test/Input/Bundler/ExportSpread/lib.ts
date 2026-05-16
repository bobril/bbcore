export function reg() {
    return { f1: ()=>"a", f2: ()=>"b", f3: ()=>"c" };
}

export let { f1: fn1, f2: fn2, f3: fn3 } = reg();
export let { f1: fn4, f2: fn5, f3: fn6 } = reg();
