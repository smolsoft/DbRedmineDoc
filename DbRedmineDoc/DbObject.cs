using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DbRedmineDoc
{
    public class DbObject : IDictionary<string, string>
    {
        private readonly string name;
        private readonly DateTime lastUpdated;

        public string Name { get => name; } 

        public DateTime LastUpdated {  get => lastUpdated; }


        private readonly Dictionary<string, string> values;
        private readonly Dictionary<string, List<Dictionary<string, string>>> tables;

        public Dictionary<string, List<Dictionary<string, string>>> Tables {  get { return tables; } }  

        public string this[string key] { get { return values[key]; } set { values[key] = value; } }

        public DbObject(string name, DateTime lastUpdated)
        {
            this.name = name;
            this.lastUpdated = lastUpdated;
            this.values = new Dictionary<string, string>();
            this.tables = new Dictionary<string, List<Dictionary<string, string>>>();
            values.Add("_name", name);
            values.Add("_updated", lastUpdated.ToString());
        }

        public List<Dictionary<string, string>> GetTableValues(string key)
        {
            return tables[key];
        }

        public void AddTable(string table)
        {
            if (!tables.ContainsKey(table))
                tables[table] = new List<Dictionary<string, string>>();
        }

        public void AddTableRow(string table, IEnumerable<(string, string)> values)
        {
            if (!tables.ContainsKey(table))
                tables[table] = new List<Dictionary<string, string>>();

            Dictionary<string, string> row = new Dictionary<string, string>();
            foreach (var tuple in values)
                row.Add(tuple.Item1, tuple.Item2);
            tables[table].Add(row);
        }

        #region IDictionary implementation
        public ICollection<string> Keys => ((IDictionary<string, string>)values).Keys;

        public ICollection<string> Values => ((IDictionary<string, string>)values).Values;

        public int Count => values.Count;

        public bool IsReadOnly => ((IDictionary<string, string>)values).IsReadOnly;

        public void Add(string key, string value)
        {
            values.Add(key, value);
        }

        public bool ContainsKey(string key)
        {
            return values.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return values.Remove(key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        {
            return values.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<string, string> item)
        {
            ((IDictionary<string, string>)values).Add(item);
        }

        public void Clear()
        {
            values.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return ((IDictionary<string, string>)values).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            ((IDictionary<string, string>)values).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return ((IDictionary<string, string>)values).Remove(item);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return ((IDictionary<string, string>)values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IDictionary<string, string>)values).GetEnumerator();
        }

        #endregion
    }

    public class DbObjectsList : List<DbObject>
    {
        private string wikiSection;
        private string templateName;

        public DbObjectsList(string wikiSection, string templateName)
        {
            this.wikiSection = wikiSection;
            this.templateName = templateName;
        }

        public string TemplateName { get => templateName;  }
        public string WikiSection { get => wikiSection;  }
    }
}
