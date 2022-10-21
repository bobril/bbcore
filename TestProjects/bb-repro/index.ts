import * as SampleModule from "./sampleModule";

const originalTest = SampleModule.test;

// @ts-ignore
SampleModule.test = function () {
    console.log("overwritten test");
    originalTest();
}

SampleModule.test();    // Should write overwritten test and original test to console
