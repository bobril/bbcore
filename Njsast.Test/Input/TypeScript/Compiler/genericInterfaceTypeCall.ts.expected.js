"use strict";
var foo;
var test;
test.fail(arg => foo.reject(arg));
test.fail2(arg => foo.reject(arg)); // Error: Supplied parameters do not match any signature of call target
