"use strict";
// @target: es2015
class I {
}
class I2 extends I {
}
var r = i[1]; // error: numeric indexer returns the type of the string indexer
var r2 = i2[1]; // error: numeric indexer returns the type of the string indexere
