using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.ConsoleRunner;
using NUnit.Core;
using NUnit.Util;

namespace reportformatter {
    public static class NunitHelper {
        public static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            try {
                return Assembly.LoadFrom(Path.Combine(Environment.CurrentDirectory, args.Name + ".dll"));
            }
            catch {
                return null;
            }
        }

        public static void CreateXmlOutput(TestResult result, TextWriter w) {
            XmlResultVisitor visitor = new XmlResultVisitor(w, result);
            result.Accept(visitor);
            visitor.Write();
        }

        public static void AddProjectService() {
        }

        public static TestRunner CreateMultipleTestDomainRunner() {
            return new MultipleTestDomainRunner(true);
        }

        public static TestResult Run(TestRunner runner, EventCollector listener, ITestFilter filter) {
            return runner.Run(listener, filter);
        }
        public static int GetFailuresCount(this ResultSummarizer summarizer) {
            return summarizer.FailureCount;
        }
        public static string GetXml(this ConsoleOptions options) {
            return options.xml;
        }
        public static NUnitProject LoadProject(string projectPath) {
            return NUnitProject.LoadProject(projectPath);
        }
        public static string GetRootSuiteName(XElement rootSuite) {
            string name = rootSuite.Attribute("name").Value;
            return name.Replace(".DLL", ".dll");
        }
        public static string GetTestPackagePath(string relativePath) {
            return relativePath;
        }
    }
}
