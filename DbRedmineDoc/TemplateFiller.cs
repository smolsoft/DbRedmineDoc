using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DbRedmineDoc
{
    public class TemplateFiller
    {
        private const string commentStart = "{{desc";
        private const string commentEnd = "}}";
        private const string webNewLine = "\n";

        private string Place(string key)
    {
            return $"\u2020{key}\u2020";
    }
        public string FillTemplate(string template, DbObject desc)
        {
            StringBuilder result = new StringBuilder(template);
            foreach (string key in desc.Keys)
                result.Replace(Place(key), desc[key]);

            foreach (var table in desc.Tables.Keys)
            {
                StringBuilder strTable = new StringBuilder();
                (string toReplace, string rowTemplate) = GetRowTemplate(template, table);
                foreach (var row in desc.Tables[table])
                {
                    StringBuilder strRow = new StringBuilder(rowTemplate);
                    foreach (var col in row.Keys)
                        strRow.Replace(Place(col), row[col]);
                    strTable.AppendLine(strRow.ToString());
                }
                // cut last newline if table have rows
                string tableString = strTable.ToString(0, strTable.Length > 0 ? strTable.Length - Environment.NewLine.Length : strTable.Length);
                result.Replace(toReplace, tableString);
            }

            return result.ToString();
        }

        public string FillTocTemplate(string template, DbObjectsList list, Func<DbObject, string, bool> include)
        {
            StringBuilder result = new StringBuilder(template);

            (string toReplace, string rowTemplate) = GetRowTemplate(template, "_toc_");
            result.Replace(toReplace, "");
            foreach (var i in list)
            {
                StringBuilder strRow = new StringBuilder(rowTemplate);
                foreach (var col in i.Keys)
                    strRow.Replace(Place(col), i[col]);
                string line = strRow.ToString();
                if (include(i, line))
                    result.AppendLine(strRow.ToString());
            }

            string resultBuilded = result.ToString();
            return template.Length < resultBuilded.Length ? resultBuilded : string.Empty;
        }

        public string UpdateTocTemplateByName(string template, DbObjectsList list, string currentContent)
        {
            StringBuilder append = new StringBuilder(template);

            (string toReplace, string rowTemplate) = GetRowTemplate(template, "_toc_");
            append.Replace(toReplace, "");

            List<string> contentLines = SplitToLines(currentContent);
            Dictionary<string, int> nameIndex = GetNameIndex(contentLines, rowTemplate);

            foreach (var i in list)
            {
                // заполняем шаблон строки
                StringBuilder strRow = new StringBuilder(rowTemplate);
                foreach (var col in i.Keys)
                    strRow.Replace(Place(col), i[col]);

                // заменяем или дополняем
                if (nameIndex.ContainsKey(i.Name))
                    contentLines[nameIndex[i.Name]] = strRow.ToString();
                else
                    append.AppendLine(strRow.ToString()); //////////// если длина append меньше шаблона из которого он сделан - значит ничего не добавляем
            }

            // собираем обновленные строки
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < contentLines.Count; i++)
                result.AppendLine(contentLines[i]);

            string resultBuilded = result.ToString() + (template.Length < append.Length ? append.ToString() : "");
            return template.Length < resultBuilded.Length ? resultBuilded : string.Empty;
        }


        private List<string> SplitToLines(string content)
        {
            List<string> result = new List<string>();
            using (StringReader sr = new StringReader(content))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                    result.Add(line);
            }
            return result;
        }

        private Dictionary<string, int> GetNameIndex(List<string> contentLines, string rowTemplate)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            
            // ищем столбец с именем
            string[] columns = rowTemplate.Split('|');
            int nameColumn = -1;
            for (int i = 0; i < columns.Length; i++)
                if (columns[i].Contains("_name"))
                {
                    nameColumn = i;
                    break;
                }

            if (nameColumn == -1)
                return result;

            // ищем смещение имени в столбце
            string nameStr = "†_name†";
            int namePos = columns[nameColumn].IndexOf(nameStr);
            int tailLen = columns[nameColumn].Length - (namePos + nameStr.Length);
            string begin = columns[nameColumn].Substring(0, namePos);
            string end = columns[nameColumn].Substring(namePos + nameStr.Length);

            // строим индекс
            for (int i = 0; i < contentLines.Count; i++)
            {
                string[] cols = contentLines[i].Split('|');
                if (cols.Length == columns.Length && cols[nameColumn].StartsWith(begin) && cols[nameColumn].EndsWith(end))
                {
                    // это строка шаблона, ищем имя
                    string name = cols[nameColumn].Substring(namePos, cols[nameColumn].Length - namePos - tailLen);
                    if (!result.ContainsKey(name))
                        result.Add(name, i);
                    else
                        Console.WriteLine($"TemplateFiller.GetNameIndex WARNING: key '{name}' duplicated");
                }
            }

            return result;
        }


        private (string toReplace, string rowTemplate) GetRowTemplate(string template, string tableName)
        {
            string[] lines = template.Split(Environment.NewLine);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith(Place(tableName)))
                {
                    string toReplace = lines[i];
                    string rowTemplate = lines[i].Substring(Place(tableName).Length);
                    return (toReplace, rowTemplate);
                }
            return (null, null);
        }

        public string MergeTemplate (string template, DbObject desc, string current)
        {
            string filled = FillTemplate(template, desc);

            var currentDesc = GetDescriptions(current);
            var placeDesc = GetDescriptions(filled);

            bool equal = currentDesc.Count == placeDesc.Count;
            if (equal)
                for (int i = 0; i < currentDesc.Count; i++)
                    if (currentDesc[i].len != placeDesc[i].len)
                    {
                        equal = false;
                        break;
                    }

            if (equal)
                return filled;

            StringBuilder merged = new StringBuilder(filled);
            List<(int placeIndex, int currentIndex)> replaceList = new List<(int, int)>();
            Func<string, int, string> startLine = (string content, int pos) =>
                {
                    int newLinePos = content.LastIndexOf(webNewLine, pos) + webNewLine.Length;
                    return content.Substring(newLinePos, pos - newLinePos);
                };

            // map placeholders with current descriptions
            int srcI = 0; // source description which will check first
            for(int j=0; j<placeDesc.Count; j++)
                for (int i = srcI; i < currentDesc.Count; i++)
                    if (startLine(filled, placeDesc[j].pos) == startLine(current, currentDesc[srcI].pos))
                    {
                        replaceList.Add((j, i));
                        srcI = i + 1;
                        break;
                    }

            // position shift prevention - replacing from end
            for (int i = replaceList.Count - 1; i >= 0; i--)
            {
                (int pos, int len) place = placeDesc[replaceList[i].placeIndex];
                (int pos, int len) cur = currentDesc[replaceList[i].currentIndex];
                merged.Remove(place.pos, place.len);
                merged.Insert(place.pos, current.Substring(cur.pos, cur.len));
            }

            return merged.ToString();
        }

        private List<(int pos, int len)> GetDescriptions(string content)
        {
            List<(int, int)> result = new List<(int, int)>();
            int pos = 0, end = 1;
            while ((pos = content.IndexOf(commentStart, end)) > -1)
            {
                end = content.IndexOf(commentEnd, pos);
                if (end > pos)
                    result.Add((pos, end + commentEnd.Length - pos));
            }
            return result;
        }
    }
}
