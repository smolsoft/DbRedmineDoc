using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DbRedmineDoc
{
    public interface IWiki
    {
        public Task<(string content, DateTime updated)> GetWikiPage(string name);

        public Task<bool> SetWikiPage(string name, string content, string parent);

    }

    public interface IMarkup
    {
        string MakeWikiLink(string text);

        string MakeImageLink(string imageName);

        string MakeSourceLink(string sourcePath);
    }
}
