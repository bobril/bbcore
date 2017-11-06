using System;
using System.Collections.Generic;
using System.Linq;

namespace Lib.Composition
{
    public class TestResultsHolder : SuiteOrTest
    {
        public string UserAgent;
        public bool Running;
        public int TestsFailed;
        public int TestsSkipped;
        public int TestsFinished;
        public int TotalTests;

        internal new TestResultsHolder Clone()
        {
            return new TestResultsHolder
            {
                Id = Id,
                ParentId = ParentId,
                IsSuite = IsSuite,
                Name = Name,
                Skipped = Skipped,
                Failure = Failure,
                Duration = Duration,
                Failures = Failures.ToList(),
                Nested = new List<SuiteOrTest>(Nested.Select(n => n.Clone())),
                Logs = Logs.ToList(),
                UserAgent = UserAgent,
                Running = Running,
                TestsFailed = TestsFailed,
                TestsFinished = TestsFinished,
                TestsSkipped = TestsSkipped,
                TotalTests = TotalTests
            };
        }
    }
}
