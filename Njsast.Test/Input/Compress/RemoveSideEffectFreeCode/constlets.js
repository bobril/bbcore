let a = 1;
const b = 2;
{
    const c = 3;
    let d = 4;
    d++;
    console.log(c, d);
}

a++;
console.log(a, b);
