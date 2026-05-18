"use strict";
// @target: es2015
// @strict: true
let obj = {};
(obj.fn) = () => 1234;
let getValue;
(getValue) = () => 1234;
let getValue2;
(getValue2) = () => 1234;
