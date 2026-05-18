"use strict";
// @target: es6
// A *self-contained* demonstration of the problem follows...
// Test this by running `tsc --target es6` on the command-line, rather than through another build tool such as Gulp, Webpack, etc.
Object.defineProperty(exports, "__esModule", { value: true });
exports.PubSubRecordIsStoredInRedisAsA = void 0;
var PubSubRecordIsStoredInRedisAsA;
(function (PubSubRecordIsStoredInRedisAsA) {
    PubSubRecordIsStoredInRedisAsA["redisHash"] = "redisHash";
    PubSubRecordIsStoredInRedisAsA["jsonEncodedRedisString"] = "jsonEncodedRedisString";
})(PubSubRecordIsStoredInRedisAsA || (exports.PubSubRecordIsStoredInRedisAsA = PubSubRecordIsStoredInRedisAsA = {}));
const buildNameFieldConstructor = (soFar) => ("name" in soFar ? {} : {
    name: (instance = undefined) => buildPubSubRecordType(Object.assign({}, soFar, { name: instance }))
});
const buildStoredAsConstructor = (soFar) => ("storedAs" in soFar ? {} : {
    storedAsJsonEncodedRedisString: () => buildPubSubRecordType(Object.assign({}, soFar, { storedAs: PubSubRecordIsStoredInRedisAsA.jsonEncodedRedisString })),
    storedAsRedisHash: () => buildPubSubRecordType(Object.assign({}, soFar, { storedAs: PubSubRecordIsStoredInRedisAsA.redisHash })),
});
const buildIdentifierFieldConstructor = (soFar) => ("identifier" in soFar || (!("record" in soFar)) ? {} : {
    identifier: (instance = undefined) => buildPubSubRecordType(Object.assign({}, soFar, { identifier: instance }))
});
const buildRecordFieldConstructor = (soFar) => ("record" in soFar ? {} : {
    record: (instance = undefined) => buildPubSubRecordType(Object.assign({}, soFar, { record: instance }))
});
const buildMaxMsToWaitBeforePublishingFieldConstructor = (soFar) => ("maxMsToWaitBeforePublishing" in soFar ? {} : {
    maxMsToWaitBeforePublishing: (instance = 0) => buildPubSubRecordType(Object.assign({}, soFar, { maxMsToWaitBeforePublishing: instance })),
    neverDelayPublishing: () => buildPubSubRecordType(Object.assign({}, soFar, { maxMsToWaitBeforePublishing: 0 })),
});
const buildType = (soFar) => ("identifier" in soFar && "object" in soFar && "maxMsToWaitBeforePublishing" in soFar && "PubSubRecordIsStoredInRedisAsA" in soFar ? {} : {
    type: soFar,
    fields: () => new Set(Object.keys(soFar)),
    hasField: (fieldName) => fieldName in soFar
});
const buildPubSubRecordType = (soFar) => Object.assign({}, buildNameFieldConstructor(soFar), buildIdentifierFieldConstructor(soFar), buildRecordFieldConstructor(soFar), buildStoredAsConstructor(soFar), buildMaxMsToWaitBeforePublishingFieldConstructor(soFar), buildType(soFar));
const PubSubRecordType = buildPubSubRecordType({});
