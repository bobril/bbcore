{const [aa, bb] = cc;}
{const [aa, [bb, cc]] = dd;}
{let [aa, bb] = cc;}
{let [aa, [bb, cc]] = dd;}
var [aa, bb] = cc;
var [aa, [bb, cc]] = dd;
var [,[,,,,,],,,zz,] = xx; // Trailing comma
var [,,zzz,,] = xxx; // Trailing comma after hole
