const a = 1;
const b = 2;
{
    const c = 3;
    const b = 4;
    console.log(c, b);
}

console.log(a, b);
