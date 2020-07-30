using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DbRedmineDoc.DbAdapters
{
    public class GitSources
    {
        private string projectRoot;
        private string srcFileMask;
        private IMarkup markup;
        public GitSources(string projectRoot, string srcFileMask, IMarkup markup)
        {
            this.projectRoot = projectRoot;
            this.srcFileMask = srcFileMask;
            this.markup = markup;
        }

        public DbObjectsList AddCallers(DbObjectsList list)
        {
            foreach (DbObject d in list)
            {
                d.AddTable("git_source");
                d["codeCalls"] = "0";
            }

            if (!Directory.Exists(projectRoot))
            {
                Console.WriteLine($"GitSources: path {projectRoot} not exist");
                return list;
            }
            var files = GetFiles(projectRoot);
            char[] mustStartWith = new char[] { ' ', '[', '.' };
            char[] mustEndWith = new char[] { ' ', ']', '(' };
            foreach(var file in files)
            {
                var content = File.ReadAllText(file);
                string gitPath = file.Substring(projectRoot.Length);
                foreach (DbObject d in list)
                {
                    int pos = content.IndexOf(d.Name);
                    string prev = pos > 0 ? content.Substring(pos - 1, 1) : "";
                    string next = pos >= 0 && pos + d.Name.Length + 1 < content.Length ? content.Substring(pos + d.Name.Length, 1) : "";
                    if (pos > 0 && prev.Trim(mustStartWith) == "" && next.Trim(mustEndWith) == "")
                    {
                        d.AddTableRow("git_source", new (string, string)[] { ("file", markup.MakeSourceLink(gitPath)) });
                        d["codeCalls"] = (int.Parse(d["codeCalls"]) + 1).ToString();
                    }
                }
            }
            
            return list;
        }

        private List<string> GetFiles(string path)
        {
            List<string> result = new List<string>();
            foreach (var dir in Directory.EnumerateDirectories(path))
                result.AddRange(GetFiles(dir));
            result.AddRange(Directory.EnumerateFiles(path, srcFileMask));
            return result;
        }
    }
}
