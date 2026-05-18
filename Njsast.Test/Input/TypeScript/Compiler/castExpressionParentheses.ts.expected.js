"use strict";
// parentheses should be omitted
// literals
({ a: 0 });
[1, 3,];
"string";
23.0;
1;
1.;
1.0;
12e+34;
0xff;
/regexp/g;
false;
true;
null;
// names and dotted names
this;
this.x;
a.x;
a;
a[0];
a.b["0"];
a().x;
1..foo;
1..foo;
1.0.foo;
12e+34.foo;
0xff.foo;
// should keep the parentheses in emit
(1.0);
(new A).foo;
(typeof A).x;
(-A).x;
new (A());
(() => { })();
(function foo() { })();
(-A).x;
// nested cast, should keep one pair of parenthese
(-A).x;
// nested parenthesized expression, should keep one pair of parenthese
(A);
