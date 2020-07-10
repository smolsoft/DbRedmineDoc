using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DbRedmineDoc.DbAdapters
{
    // Добавляет в таблицу результаты тестирования
    class TestResults
    {
        class TestResult
        {
            public string name;
            public string transform;
            public List<TestCall> testCalls;
            public bool result
            {
                get
                {
                    bool ok = true;
                    if (testCalls != null)
                        foreach (TestCall c in testCalls)
                            ok = ok && c.compareResult.Contains("==") && string.IsNullOrEmpty(c.error);
                    return ok;
                }
            }

            public int avgTimeMS
            {
                get { return testCalls == null ? 0 : (int)testCalls.Select(c => int.Parse(c.timeMS)).Average(); }
            }

            public int avgTimePG
            {
                get { return testCalls == null ? 0 : (int)testCalls.Select(c => int.Parse(c.timePG)).Average(); }
            }

        }

        class TestCall
        {
            public string msQuery;
            public string pgQuery;
            public string compareResult;
            public string timeMS;
            public string timePG;
            public string error;
        }

        private readonly string testReportPath;
        private readonly string testQueriesPath;

        public TestResults(string testReportPath, string testQueriesPath)
        {
            this.testReportPath = testReportPath;
            this.testQueriesPath = testQueriesPath;
        }


        public DbObjectsList AddTestResult(DbObjectsList list)
        {
            Dictionary<string, TestResult> tests = ParseTestResults(testReportPath);
            DateTime updated = File.GetLastWriteTime(testReportPath);

            foreach (DbObject d in list)
                if (tests.ContainsKey(d.Name))
                {
                    TestResult test = tests[d.Name];
                    d["pgCallTransform"] = test.transform;
                    d["pgTestTimeDiffA"] = $"{test.avgTimePG - test.avgTimeMS} ms";
                    d["pgTestResult"] = test.result ? "OK" : "FAILED";
                    foreach (var t in test.testCalls)
                        d.AddTableRow("testResult", new (string, string)[] {
                            ("compareResults", t.compareResult),
                            ("testMStime", $"{t.timeMS} ms"),
                            ("testPGtime", $"{t.timePG} ms"),
                            ("error", t.error),
                            ("msQuery", t.msQuery),
                            ("pgQuery", t.pgQuery),
                        });
                    if (d.LastUpdated < updated)
                        d.LastUpdated = updated;
                }
                else
                {
                    d.AddTable("testResult");
                    d["pgCallTransform"] = "";
                    d["pgTestResult"] = "";
                    d["pgTestTimeDiffP"] = "";
                    d["pgTestTimeDiffA"] = "";
                    d["pgError"] = "";
                    d["pgTestResult"] = "";
                }
            return list;
        }

        // $"{test.file}\t{test.procName}\t{compareCalls}\t{compareResults}\t{compareTimePercent}\t{compareTimeAbs}\t{error}");
        private Dictionary<string, TestResult> ParseTestResults(string testReportPath)
        {
            Dictionary<string, TestResult> tests = new Dictionary<string, TestResult>();
            string[] lines = File.ReadAllLines(testReportPath);
            string[] queries = File.ReadAllLines(testQueriesPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                string[] qParts = queries[i].Split('\t');
                TestResult tr;
                if (!tests.ContainsKey(parts[1]))
                {
                    tr = new TestResult();
                    tr.name = parts[1];
                    tr.transform = parts[2];
                    tr.testCalls = new List<TestCall>();
                    tests.Add(parts[1], tr);
                }
                else
                    tr = tests[parts[1]];

                tr.testCalls.Add(new TestCall
                {
                    compareResult = parts[3],
                    timeMS = parts[4],
                    timePG = parts[5],
                    error = parts[6],
                    msQuery = qParts[0],
                    pgQuery = qParts[1]
                });
            }
            return tests;
        }
    }
}
