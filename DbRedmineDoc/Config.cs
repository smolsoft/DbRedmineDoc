﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DbRedmineDoc
{
    public class Config
    {
        public string RedmineKey { get; set; }

        public string DbConnectionString { get; set; }

        public string RedmineRoot { get; set; }

        public string Steps { get; set; }

        public string GitRootPath { get; set; }

        public string GitSourcesMask { get; set; }

        public string PgGitRootPath { get; set; }

        public string TestResultFilePath { get; set; }

        public string TestQueriesPath { get; set; }
    }
}
