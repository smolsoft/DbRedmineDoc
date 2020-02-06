using System;
using System.Collections.Generic;
using System.Text;

namespace DbRedmineDoc.WikiAdapters
{
    public class TextileMarkup : IMarkup
    {
        public string MakeWikiLink(string text)
        {
            return $"[[{text}]]";
        }

        public string MakeImageLink(string imageName)
        {
            return $"!/dbdoc/{imageName}.png!";
        }

        public string MakeSourceLink(string sourcePath)
        {
            return $"source:{sourcePath}";
        }

        public string MakeItalic(string text)
        {
            return $"_{text}_";
        }
    }
}
