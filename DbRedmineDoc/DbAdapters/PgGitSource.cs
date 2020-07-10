using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DbRedmineDoc.DbAdapters
{
    // Отмечает процедуры, имеющие исходник для Постгреса
    class PgGitSource
    {
        private string projectRoot;
        private string srcFileMask;
        private IMarkup markup;
        public PgGitSource(string projectRoot, string srcFileMask, IMarkup markup)
        {
            this.projectRoot = projectRoot;
            this.srcFileMask = srcFileMask;
            this.markup = markup;
        }

        public DbObjectsList AddMarks(DbObjectsList list)
        {
            var files = GetFiles(projectRoot);
            foreach (DbObject d in list)
            {
                string fileName = files
                    .Where(f => string.Equals(d.Name, Path.GetFileNameWithoutExtension(f), StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault();
                d["pgGitSourceMark"] = string.IsNullOrEmpty(fileName) ? "" : "X";
                d["pgGitSourceLink"] = string.IsNullOrEmpty(fileName) ? "" : 
                    markup.MakeSourceLink(fileName.Substring(projectRoot.Length));
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
