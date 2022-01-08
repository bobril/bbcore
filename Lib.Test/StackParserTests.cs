using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lib.Test;

public class StackParserTests
{
    [Fact]
    public void StackParserDoesNotCrashOnComplexInput()
    {
        var input = "Error: Expected function to throw an exception with a message matching /!!!Multi-model transactions are not supported/, but it threw an exception with message '[CriticalFailure] in 1/2.0.0(Default Main Model)\nMulti-model transactions are not supported. Recording History of model:1/1.0.0(Default Main Model), in state:Write-recording. Attempted to write to model:1/2.0.0(Default Main Model) Error\n at new ModelManagementError (http://localhost:8080/testbundle.js:171940:123)\n at Object.isHistoryRecordingAndEnsureCanWrite (http://localhost:8080/testbundle.js:161931:46)\n at AppModel.<anonymous> (http://localhost:8080/testbundle.js:168907:49)\n at http://localhost:8080/testbundle.js:54253:79\n at Object.undoableAction (http://localhost:8080/testbundle.js:161969:9)\n at http://localhost:8080/testbundle.js:54249:76\n at compare (http://localhost:8080/jasmine-core.js:3835:11)\n at Expectation.toThrowError (http://localhost:8080/jasmine-core.js:2348:35)\n at http://localhost:8080/testbundle.js:54255:60\n at step (http://localhost:8080/testbundle.js:75:23)'.\n at stack (http://localhost:8080/jasmine-core.js:2455:17)\n at buildExpectationResult (http://localhost:8080/jasmine-core.js:2425:14)\n at Spec.expectationResultFactory (http://localhost:8080/jasmine-core.js:901:18)\n at Spec.addExpectationResult (http://localhost:8080/jasmine-core.js:524:34)\n at Expectation.addExpectationResult (http://localhost:8080/jasmine-core.js:845:21)\n at Expectation.toThrowError (http://localhost:8080/jasmine-core.js:2369:12)\n at http://localhost:8080/testbundle.js:54255:60\n at step (http://localhost:8080/testbundle.js:75:23)\n at Object.next (http://localhost:8080/testbundle.js:56:53)\n at fulfilled (http://localhost:8080/testbundle.js:46:58)";
        Composition.StackFrame.Parse(input);
    }
}