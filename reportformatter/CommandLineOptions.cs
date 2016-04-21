using CommandLine;

namespace reportformatter {
    class CommandLineOptions {
        [Option('s', "source", HelpText = "NUnit source report file name", Required = true)]
        public string SourceReportName { get; set; }
        [Option('t', "target", HelpText = "NUnit target report file name", Required = true)]
        public string TargetReportName { get; set; }
    }
}
