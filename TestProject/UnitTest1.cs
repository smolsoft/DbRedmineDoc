using System;
using System.IO;
using NUnit.Framework;
using DbRedmineDoc;

namespace TestProject
{
    public class Tests
    {
        private DbObject obj;
        private TemplateFiller filler;

        [SetUp]
        public void Setup()
        {
            this.filler = new TemplateFiller();
            obj = new DbObject("test", DateTime.Now);
            obj["prop1"] = "value1";
            obj["prop2"] = "value2 value2 value2";
            obj.AddTableRow("table1", new (string, string)[] { ("col1", "r1c1"), ("col2", "col2"), ("col3", "r1c3") });
            obj.AddTableRow("table1", new (string, string)[] { ("col1", "r2c1"), ("col2", ""), ("col3", "r2c3") });
            obj.AddTableRow("table1", new (string, string)[] { ("col1", "r3c1"), ("col2", "col2"),     ("col3", "r3c3") });
            obj.AddTableRow("table2", new (string, string)[] { ("col1", "Tr1c1"), ("col2", "T2r1c2"), ("col3", "col3") });
            obj.AddTableRow("table2", new (string, string)[] { ("col1", "Tr2c1"), ("col2", "T2r2c2"), ("col3", "") });
            obj.AddTableRow("table2", new (string, string)[] { ("col1", "Tr3c1"), ("col2", "T2r3c2"), ("col3", "col3col3") });
            obj.AddTableRow("table2", new (string, string)[] { ("col1", "Tr3c1"), ("col2", "T2r4c2"), ("col3", "") });
        }

        [Test]
        public void TestSimpleFill()
        {
            (string template, string current, string correct) = GetStrings("fill");
            Assert.AreEqual(correct, filler.FillTemplate(template, obj));
        }
        [Test]
        public void TestSimpleMerge()
        {
            (string template, string current, string correct) = GetStrings("simple");
            Assert.AreEqual(correct, filler.MergeTemplate(template, obj, current));
        }


        private (string template, string current, string correct) GetStrings(string testName)
        {
            string template = File.ReadAllText(@"Templates\template.txt");
            string current = File.ReadAllText(@"Current\" + testName + ".txt");
            string correct = File.ReadAllText(@"Correct\" + testName + ".txt");
            return (template, current, correct);
        }
    }
}