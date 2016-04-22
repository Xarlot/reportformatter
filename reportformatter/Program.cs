using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using CommandLine;
using NLog;


namespace reportformatter {
    class Program {
        static Dictionary<string, Info> infos = new Dictionary<string, Info>();
        static Dictionary<string, RunInfo> runInfos = new Dictionary<string, RunInfo>();

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args) {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            var exitCode = result.MapResult(clo => {
                try {
                    return DoWork(clo);
                }
                catch (Exception ex) {
                    Log.Error(ex, "Exception thrown...");
                    return 1;
                }
            },
            errors => 1);
            Environment.Exit(exitCode);
        }

        static int DoWork(CommandLineOptions clo) {
            string currentDir = Directory.GetCurrentDirectory();
            string sourceFile = Path.Combine(currentDir, clo.SourceReportName);
            if (!File.Exists(sourceFile)) {
                Log.Error($"Can`t find source file {sourceFile}");
                return 1;
            }
            string targetFilePath = Path.Combine(currentDir, clo.TargetReportName);

            ProcessFile(targetFilePath, sourceFile);
            return SaveReport(targetFilePath, clo.TargetReportName);
        }

        static bool ProcessFile(string targetFilePath, string sourceFilePath) {
            string outFileName = targetFilePath;
            if (!infos.ContainsKey(outFileName))
                infos.Add(outFileName, new Info("root"));
            bool orgReport = false;
            if (!runInfos.ContainsKey(outFileName)) {
                RunInfo ri = new RunInfo();
                runInfos.Add(outFileName, ri);
            }
            var filesToGlue = new[] { sourceFilePath };
            foreach (var item in filesToGlue) {
                Log.Info("glue " + item);

                XDocument xml = XDocument.Load(item);
                XElement tmpEl = xml.Root;
                var total = int.Parse(tmpEl.Attribute("total").Value);
                var not_run = int.Parse(tmpEl.Attribute("not-run").Value);
                var failures = int.Parse(tmpEl.Attribute("failures").Value);
                runInfos[outFileName].Plus(total, failures, not_run);
                //infos[outFilePath] = new Info();
                IEnumerable<XElement> elements;

                if (orgReport)
                    elements = xml.Root.Elements("test-suite");
                else
                    elements = xml.Root.Element("test-suite").Element("results").Elements("test-suite");
                foreach (XElement main in elements) {
                    Info resultInfo;
                    string rootSuiteName = main.Attribute("name").Value;
                    if (infos[outFileName].DescendantDict.ContainsKey(rootSuiteName)) {
                        resultInfo = ParseDescendantsRecursive(main, infos[outFileName].DescendantDict[rootSuiteName]);
                        infos[outFileName].DescendantDict[rootSuiteName] = resultInfo;
                    }
                    else {
                        resultInfo = ParseDescendantsRecursive(main, null);
                        infos[outFileName].DescendantDict.Add(rootSuiteName, resultInfo);
                    }
                    Log.Info(rootSuiteName);
                }
            }
            foreach (var subRootInfo in infos[outFileName].DescendantDict.Values) {
                infos[outFileName].Success = infos[outFileName].Success && subRootInfo.Success;
            }
            return infos[outFileName].Success;
        }

        static Info ParseDescendantsRecursive(XElement rootSuite, Info info) {
            bool foundSuites = false;
            string rootSuiteName = NunitHelper.GetRootSuiteName(rootSuite);
            

            bool rootSuccess = true;
            var succAttribute = rootSuite.Attribute("success");
            if (succAttribute != null) {
                rootSuccess = succAttribute.Value.ToLowerInvariant() == "true";
            }
            double rootTime = 0d;
            var timeAttribute = rootSuite.Attribute("time");
            if (timeAttribute != null)
                rootTime = double.Parse(timeAttribute.Value, NumberFormatInfo.InvariantInfo);
            int rootAsserts = 0;
            var assertsAttribute = rootSuite.Attribute("asserts");
            if (assertsAttribute != null)
                rootAsserts = int.Parse(assertsAttribute.Value);

            var resultsElement = rootSuite.Element("results");
            if (resultsElement == null) {
                resultsElement = new XElement("results");
                rootSuite.Add(resultsElement);
            }

            string suteResult = "default";
            var resultAttibute = rootSuite.Attribute("result");
            if (resultAttibute != null)
                suteResult = resultAttibute.Value.ToLowerInvariant();
            if (suteResult == "inconclusive" && !rootSuccess) {
                succAttribute.SetValue("true");
                rootSuccess = true;
            }
            if (suteResult == "ignored" && !rootSuccess) {
                string executed = "true";
                var executedAttibute = rootSuite.Attribute("executed");
                if (executedAttibute != null)
                    executed = executedAttibute.Value.ToLowerInvariant();
                if (executed == "false")
                    rootSuccess = true;
            }

            if (info == null) {
                info = new Info(rootSuiteName, rootSuccess, rootTime, rootAsserts) { Result = suteResult };
            }
            else {
                if (info.SuiteName == rootSuiteName) {
                    if (rootSuccess != info.Success)
                        info.Success = false;
                    info.Time += rootTime;
                    info.Asserts += rootAsserts;
                }
                else
                    //return null; //
                    throw new Exception("!!!");
            }
            // parse descendants
            foreach (var tmp in resultsElement.Elements("test-suite")) {
                string suiteName = tmp.Attribute("name").Value;
                Info result;
                if (info.DescendantDict.ContainsKey(suiteName))
                    result = ParseDescendantsRecursive(tmp, info.DescendantDict[suiteName]);
                else
                    result = ParseDescendantsRecursive(tmp, null);
                if (result != null) {
                    if (info.DescendantDict.ContainsKey(suiteName)) {
                        info.DescendantDict[suiteName] = result;
                    }
                    else {
                        info.DescendantDict.Add(suiteName, result);
                    }
                }
                else {
                    throw new Exception("invalid situation in ParseDescendantsRecursive");
                }
                foundSuites = true;
            }
            if (!foundSuites) {
                System.Xml.XmlReader reader = resultsElement.CreateReader();
                reader.MoveToContent();

                string innerXml = reader.ReadInnerXml(); /* ReadInnerXml */

                if (string.IsNullOrEmpty(info.InnerTestCases))
                    info.InnerTestCases = "<results>";

                info.InnerTestCases += innerXml;
                //foreach(XElement main in rootSuite.Descendants("test-case")) {
                //    info.InnerTestCases.Add(main.ToString());
                //}
            }
            return info;
        }
        static XElement ParseInfoRecursive(Info inf) {
            XElement testSuite = new XElement("test-suite", new XAttribute("name", inf.SuiteName),
                                                                   new XAttribute("success", inf.Success),
                                                                   new XAttribute("time", inf.Time.ToString("#####0.000", NumberFormatInfo.InvariantInfo)),
                                                                   new XAttribute("asserts", inf.Asserts.ToString()),
                                                                   new XAttribute("result", inf.Result.ToString()));
            //mainElement.Add("results");
            //XElement el = mainElement.Element("results");
            if (inf.DescendantDict.Count == 0) {
                if (string.IsNullOrEmpty(inf.InnerTestCases))
                    throw new Exception("inf.innerTestCases.Count = 0 && inf.descendantDict.Count == 0");
                else {
                    //mainElement.CreateWriter() ?
                    XElement results = XElement.Parse(inf.InnerTestCases + "</results>");
                    testSuite.Add(results);
                }
                //XNode xfrag = XDocument.ReadFrom();
                //XmlDocument document = new XmlDocument();
                //document.Load("contosoBooks.xml");
                //XPathNavigator navigator = document.CreateNavigator();
                //xfrag.
                //    xfrag.InnerXml = @"<Demographic><Age/><DOB/></Demographic>";

                //    xdoc.DocumentElement.FirstChild.AppendChild(xfrag);
            }
            else {
                XElement mainElement = new XElement("results");
                foreach (var item in inf.DescendantDict.Keys) {
                    mainElement.Add(ParseInfoRecursive(inf.DescendantDict[item]));
                }
                testSuite.Add(mainElement);
            }
            return testSuite;
        }

        static XElement InfoToXml(string id) {
            if (!infos.ContainsKey(id))
                throw new Exception("InfoToXml error. " + id + " not found in infos");
            Info info = infos[id];
            return ParseInfoRecursive(info);
        }
        static int SaveReport(string filePath, string nunitTestSetName, string enviroment = null) {
            Log.Info("saving final report in " + filePath + "...");
            string rootDir = Path.GetDirectoryName(filePath);
            string date = DateTime.Now.ToShortDateString();
            string time = DateTime.Now.ToLongTimeString();

            int failures = 0;
            int total = 0;
            int notRun = 0;
            foreach (var item in runInfos.Keys) {
                RunInfo runInfo = runInfos[item];
                failures += runInfo.Failures;
                notRun += runInfo.Not_run;
                total += runInfo.Total;
            }
            XElement root = new XElement("test-results", new XAttribute("name", nunitTestSetName),
                                                         new XAttribute("failures", failures.ToString()),
                                                         new XAttribute("total", total.ToString()),
                                                         new XAttribute("not-run", notRun.ToString()),
                                                         new XAttribute("date", date),
                                                         new XAttribute("time", time));
            if (enviroment != null) { // todo

            }
            //root.Add("culture-info", new XAttribute("current-culture", "en-US"),  new XAttribute("current-uiculture", "en-US") ); // todo
            XDocument resultDoc = new XDocument();
            //XElement tmp = resultDoc.Element("results");
            foreach (var key in infos.Keys) {
                //string suiteNameRoot = rootDir + "\\" + key + ".dll";
                root.Add(InfoToXml(key));
            }
            resultDoc.Add(root);

            resultDoc.Save("tmpReport"/*, SaveOptions.None*/);
            File.Copy("tmpReport", filePath);
            File.OpenRead(filePath).Close();
            Console.WriteLine("saved");
            return failures;
        }
    }
}
