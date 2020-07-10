using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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
                .AddJsonFile("config.json")
                .AddCommandLine(args);
            
            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) ||
                                devEnvironmentVariable.ToLower() == "development";
            if (isDevelopment)
                builder.AddUserSecrets<Program>();

            IConfigurationSection configSection = builder.Build().GetSection("config");
            configSection.Bind(cfg);

            // init what to do
            Dictionary<string, bool> steps = 
                new Dictionary<string, bool>(cfg.Steps.Split(',').Select(s => new KeyValuePair<string, bool>(s, true)));

            // init services
            IWiki wiki = new Redmine(cfg.RedmineKey, cfg.RedmineRoot);
            IMarkup markup = new TextileMarkup();

            // init DB adapters
            MsSqlDb db = new MsSqlDb(cfg.DbConnectionString, markup);
            GitSources git = new GitSources(cfg.GitRootPath, cfg.GitSourcesMask, markup);
            PgGitSource pgGit = new PgGitSource(cfg.PgGitRootPath, "*.sql", markup);
            TestResults tests = new TestResults(cfg.TestResultFilePath, cfg.TestQueriesPath);

            // init wiki sections
            DbObjectsList tables = new DbObjectsList("Таблицы_AICS_AreaPassport", "table.txt");
            DbObjectsList routines = new DbObjectsList("Программы_AICS_AreaPassport", "routine.txt");

            // collect data
            if (steps.ContainsKey("Tables"))
                db.GetTables(tables);
            if (steps.ContainsKey("Routines"))
                db.GetRoutines(routines);
            if (steps.ContainsKey("CheckSources"))
                git.AddCallers(routines);
            if (steps.ContainsKey("CheckPostgres"))
                pgGit.AddMarks(routines);
            if (steps.ContainsKey("CheckTests"))
                tests.AddTestResult(routines);

            Console.WriteLine($"got {tables.Count} tables and {routines.Count} routines");

            // save to wiki
            try
            {
                Task saveTables = steps.ContainsKey("Tables") ? SaveSection(tables, wiki) : Task.CompletedTask;
                Task saveRoutines = steps.ContainsKey("Routines") ? SaveSection(routines, wiki) : Task.CompletedTask;
                Task.WaitAll(saveTables, saveRoutines);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Console.WriteLine("done");            
        }


        private static async Task<bool> SaveSection(DbObjectsList list, IWiki wiki)
        {
            TemplateFiller tf = new TemplateFiller();

            // table of contents
            var section = await wiki.GetWikiPage(list.WikiSection);
            //string toc = tf.FillTocTemplate(File.ReadAllText($"Templates/toc_{list.TemplateName}"), list, (desc, line) => !section.content.Contains(line));
            string toc = tf.UpdateTocTemplateByName(File.ReadAllText($"Templates/toc_{list.TemplateName}"), list, section.content);
            if (!string.IsNullOrEmpty(toc))
                await wiki.SetWikiPage(list.WikiSection, toc, null);
                //await wiki.SetWikiPage(list.WikiSection, section.content + Environment.NewLine + toc, null);
            Console.WriteLine($"in section {list.WikiSection} TOC saved");

            // pages for each object
            string template = File.ReadAllText($"Templates/{list.TemplateName}");
            int created = 0, updated = 0, skipped = 0;
            foreach (var i in list)
            {
                var currentPage = await wiki.GetWikiPage(i.Name);

                if (string.IsNullOrEmpty(currentPage.content))
                {// create page
                    await wiki.SetWikiPage(i.Name, tf.FillTemplate(template, i), list.WikiSection);
                    created++;
                }
                else if (i.LastUpdated > currentPage.updated)
                {// update page
                    await wiki.SetWikiPage(i.Name, tf.MergeTemplate(template, i, currentPage.content), list.WikiSection);
                    updated++;
                }
                else
                    skipped++;// nothing changed - skip page
            }
            Console.WriteLine($"in section {list.WikiSection} created {created}, updated {updated}, skipped {skipped} pages");
            return true;
        }
    }
}
