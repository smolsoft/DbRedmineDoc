using Newtonsoft.Json;
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

        public string GitRootPath { get; set; }

        public string GitSourcesMask { get; set; }
    }
}
