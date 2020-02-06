using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using DbRedmineDoc.DbAdapters;
using DbRedmineDoc.WikiAdapters;

namespace DbRedmineDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            // init config
            Config cfg = new Config();
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json");

            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) ||
                                devEnvironmentVariable.ToLower() == "development";
            if (isDevelopment)
                builder.AddUserSecrets<Program>();

            IConfigurationSection configSection = builder.Build().GetSection("config");
            configSection.Bind(cfg);

            // init services
            IWiki wiki = new Redmine(cfg.RedmineKey, cfg.RedmineRoot);
            IMarkup markup = new TextileMarkup();

            // init DB adapters
            MsSqlDb db = new MsSqlDb(cfg.DbConnectionString, markup);
            GitSources git = new GitSources(cfg.GitRootPath, cfg.GitSourcesMask, markup);

            // init wiki sections
            DbObjectsList tables = new DbObjectsList("Таблицы_AICS_AreaPassport", "table.txt");
            DbObjectsList routines = new DbObjectsList("Программы_AICS_AreaPassport", "routine.txt");

            // collect data
            db.GetTables(tables);
            db.GetRoutines(routines);
            git.AddCallers(routines);

            // save to wiki
            Task.WaitAll(
                SaveSection(tables, wiki), 
                SaveSection(routines, wiki));
            
        }


        private static async Task<bool> SaveSection(DbObjectsList list, IWiki wiki)
        {
            TemplateFiller tf = new TemplateFiller();

            // table of contents
            var section = await wiki.GetWikiPage(list.WikiSection);
            string toc = tf.FillTocTemplate(File.ReadAllText($"Templates/toc_{list.TemplateName}"), list,
                (desc, line) => !section.content.Contains(line));
            if (!string.IsNullOrEmpty(toc))
                await wiki.SetWikiPage(list.WikiSection, section.content + Environment.NewLine + toc, null);

            // pages for each object
            string template = File.ReadAllText($"Templates/{list.TemplateName}");
            foreach (var i in list)
            {
                var currentPage = await wiki.GetWikiPage(i.Name);

                if (string.IsNullOrEmpty(currentPage.content)) // create page
                    await wiki.SetWikiPage(i.Name, tf.FillTemplate(template, i), list.WikiSection);
                else if (i.LastUpdated > currentPage.updated) // update page
                    await wiki.SetWikiPage(i.Name, tf.MergeTemplate(template, i, currentPage.content), list.WikiSection);
                else
                    ;// nothing changed - skip page
            }
            return true;
        }
    }
}
