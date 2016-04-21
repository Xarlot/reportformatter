using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace reportformatter {
    public class RunInfo {
        int not_run;
        int total;
        int failures;
        public void Plus(int total, int failures, int not_run) {
            this.not_run += not_run;
            this.total += total;
            this.failures += failures;
        }
        public RunInfo() {
            not_run = 0;
            total = 0;
            failures = 0;
        }
        public int Failures { get { return failures; } }
        public int Not_run { get { return not_run; } }
        public int Total { get { return total; } }
    }
    public class Info {
        public string SuiteName; // for debug
        public bool Success;
        public double Time;
        public int Asserts;
        public string Result = String.Empty;
        //public List<string> innerTestCases;
        public string InnerTestCases;
        public Dictionary<string, Info> DescendantDict;
        public Info(string name) {
            this.SuiteName = name;
            this.DescendantDict = new Dictionary<string, Info>();
            this.Success = true;
            this.Time = 0;
            this.Asserts = 0;
            InnerTestCases = string.Empty;
        }
        public Info(string _namespace, bool succes, double time, int asserts) {
            this.SuiteName = _namespace;
            DescendantDict = new Dictionary<string, Info>();
            this.Success = succes;
            this.Time = time;
            this.Asserts = asserts;
            InnerTestCases = string.Empty; //new List<string>();
        }
    }
    public enum NUnitDomainUsage {
        Default = 0,
        None = 1,
        Single = 2,
        Multiple = 3,
    }
}
