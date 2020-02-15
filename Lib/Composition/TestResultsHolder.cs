using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Newtonsoft.Json;

namespace Lib.Composition
{
    public class TestResultsHolder : SuiteOrTest
    {
        public string UserAgent;
        public bool Running;
        [JsonIgnore]
        public uint[]? CoverageData;
        public int SuitesFailed;
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
                TotalTests = TotalTests,
                CoverageData = CoverageData
            };
        }

        static void WriteJUnitSystemOut(XmlWriter w, SuiteOrTest test)
        {
            if (test.Skipped)
            {
                w.WriteStartElement("skipped");
                w.WriteEndElement();
            }
            else if (test.Failure)
            {
                test.Failures.ForEach(fail =>
                {
                    w.WriteStartElement("failure");
                    w.WriteAttributeString("message", fail.Message + "\n" + string.Join("\n", fail.Stack));
                    w.WriteEndElement();
                });
            }
            if (test.Logs == null || test.Logs.Count == 0)
                return;
            w.WriteStartElement("system-out");
            w.WriteAttributeString("xml", "space", null, "preserve");
            test.Logs.ForEach(m =>
            {
                w.WriteString(m.Message + "\n  " + string.Join("\n  ", m.Stack) + "\n");
            });
            w.WriteEndElement();
        }

        static void WriteTestCases(XmlWriter w, SuiteOrTest suite)
        {
            suite.Nested.ForEach(test =>
            {
                if (test.IsSuite)
                    return;
                w.WriteStartElement("testcase");
                w.WriteAttributeString("name", test.Name);
                w.WriteAttributeString("time", (test.Duration * 0.001).ToString("F4", CultureInfo.InvariantCulture));
                WriteJUnitSystemOut(w, test);
                w.WriteEndElement();
            });
        }

        static void RecursiveWriteJUnit(XmlWriter w, SuiteOrTest suite, string name, bool isWritingFlatTestSuites)
        {
            var duration = 0d;
            var testCaseCount = 0;
            var flat = true;
            if (suite.Nested != null)
            {
                suite.Nested.ForEach(n =>
                {
                    if (n.IsSuite)
                    {
                        flat = false;
                        return;
                    }
                    testCaseCount++;
                    duration += n.Duration;
                });
            }
            if (flat || !isWritingFlatTestSuites)
                duration = suite.Duration;
            var isRecursiveTestCaseToWrite = !isWritingFlatTestSuites && !string.IsNullOrEmpty(name);
            if (testCaseCount > 0 || isRecursiveTestCaseToWrite)
            {
                w.WriteStartElement("testsuite");
                w.WriteAttributeString("name", string.IsNullOrEmpty(name) ? "root" : name);
                w.WriteAttributeString("time", (duration * 0.001).ToString("F4", CultureInfo.InvariantCulture));

                WriteTestCases(w, suite);

                if (!flat && !isWritingFlatTestSuites)
                {
                    if (suite.Nested != null) suite.Nested.ForEach(n =>
                      {
                          if (n.IsSuite)
                          {
                              RecursiveWriteJUnit(w, n, n.Name, false);
                          }
                      });
                }
                WriteJUnitSystemOut(w, suite);
                w.WriteEndElement();
            }
            if (!flat && (isWritingFlatTestSuites || !isRecursiveTestCaseToWrite))
            {
                if (suite.Nested != null) suite.Nested.ForEach(n =>
                  {
                      if (n.IsSuite)
                      {
                          RecursiveWriteJUnit(w, n, (!string.IsNullOrEmpty(name) ? name + "." : "") + n.Name, isWritingFlatTestSuites);
                      }
                  });
            }
        }

        public sealed class StringWriterWithUtf8Encoding : StringWriter
        {
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }

        public string ToJUnitXml(bool flatTestSuites)
        {
            var sw = new StringWriterWithUtf8Encoding();
            var w = new XmlTextWriter(sw);
            w.WriteStartDocument();
            w.WriteStartElement("testsuites");
            w.WriteAttributeString("errors", "" + SuitesFailed);
            w.WriteAttributeString("failures", "" + TestsFailed);
            w.WriteAttributeString("tests", "" + TotalTests);
            w.WriteAttributeString("time", (Duration * 0.001).ToString("F4", CultureInfo.InvariantCulture));
            RecursiveWriteJUnit(w, this, "", flatTestSuites);
            WriteJUnitSystemOut(w, this);
            w.WriteEndElement();
            w.WriteEndDocument();
            return sw.ToString();
        }
    }
}
