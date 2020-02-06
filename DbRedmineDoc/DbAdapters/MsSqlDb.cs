using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Data.Common;

namespace DbRedmineDoc.DbAdapters
{
    public class MsSqlDb
    {
        private readonly string connString;
        private readonly IMarkup markup;

        private readonly BaseDataAccess db;

        private readonly string[] skipPrefix = new string[] { "sys", "msmerge", "_", "sp_" };

        private const string qTableList = "SELECT TABLE_SCHEMA, TABLE_NAME, create_date, modify_date " +
            "FROM INFORMATION_SCHEMA.TABLES t " +
            "LEFT JOIN sys.objects o ON o.name = t.TABLE_NAME WHERE TABLE_TYPE= 'BASE TABLE' ORDER BY TABLE_NAME";
        private const string qTableColumns = "SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH " +
            "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{0}' ORDER BY ORDINAL_POSITION";

        private const string qRoutinesList = "SELECT LTRIM(REPLACE(' ' + REPLACE(' ' + ROUTINE_NAME, ' fn_', ''), ' clrfn_', '')), " +
            "ROUTINE_NAME, ROUTINE_TYPE, DATA_TYPE, ROUTINE_BODY, SQL_DATA_ACCESS, LAST_ALTERED, ROUTINE_SCHEMA, " +
            "(LEN(ROUTINE_DEFINITION)-LEN(REPLACE(ROUTINE_DEFINITION, char(10),'')))/10+1 loc10 " + 
            "FROM INFORMATION_SCHEMA.ROUTINES order by 1";

        private const string qRoutineReferenced = "SELECT db.name dbname, referenced_entity_name, MAX(CAST(is_updated AS int)) " +
            "FROM sys.dm_sql_referenced_entities(N'{0}.{1}', 'OBJECT')  " +
            "LEFT JOIN sys.databases db on referenced_database_name = db.name " +
            "WHERE referenced_id IS NOT NULL OR db.name IS NOT NULL " +
            "GROUP BY db.name, referenced_entity_name " + 
            "ORDER BY referenced_entity_name";

        private const string qRoutineReferencing = "SELECT referencing_entity_name " +
            "FROM sys.dm_sql_referencing_entities (N'{0}.{1}', 'OBJECT')";

        private const string qRoutineParams = "SELECT parameter_id, p.name, t.name, p.max_length, is_output " +
            "FROM sys.parameters p LEFT JOIN sys.types t ON t.system_type_id = p.system_type_id  AND t.user_type_id = p.user_type_id " +
            "WHERE object_id = object_id('{0}.{1}') " +
            "ORDER BY parameter_id";

        private const string qRoutineFuncResult = "SELECT * FROM sys.dm_exec_describe_first_result_set_for_object (OBJECT_ID('{0}'), 0) " +
            "ORDER BY column_ordinal";

        private const string T_COLUMNS = "columns";
        private const string T_FOREIGN = "foreign";
        private const string T_USED = "used";
        private const string T_CALL = "call";
        private const string T_CALLEE = "callee";
        private const string T_PARAMS = "params";
        private const string T_RESULT = "result";

        private readonly string[] setTable = new string[] { T_COLUMNS, T_FOREIGN, T_USED };
        private readonly string[] setRoutine = new string[] { T_CALL, T_CALLEE, T_PARAMS, T_RESULT };

        public MsSqlDb(string connString, IMarkup markup)
        {
            this.connString = connString;
            this.markup = markup;
            this.db = new BaseDataAccess(connString);
        }

        private bool Skip(string str)
        {
            for (int i = 0; i < skipPrefix.Length; i++)
                if (str.ToLower().StartsWith(skipPrefix[i]))
                    return true;
            return false;
        }

        #region Tables

        public DbObjectsList GetTables(DbObjectsList list)
        {
            foreach (DbObject d in GetTablesList(list))
            {
                // init tables as empty
                foreach (string name in setTable)
                    d.AddTable(name);

                // read FK cols
                Dictionary<string, string> FKs = GetFKsList(d);

                // read columns
                FillTable(T_COLUMNS, string.Format(qTableColumns, d.Name), (r, table) =>
                {
                    string colName = r.GetString(1);
                    d.AddTableRow(table, new (string, string)[]
                    {
                        ("col_i", r.GetInt32(0).ToString()),
                        ("col_name", colName),
                        ("col_type", r.GetString(2)),
                        ("col_size", r.IsDBNull(3) ? "" : r.GetInt32(3).ToString()),
                        ("col_fk", FKs.ContainsKey(colName) ? "FK: " + markup.MakeWikiLink(FKs[colName]) : "")
                    });
                });

                // read tables where used
                FillTable(T_FOREIGN, db.GetDataReader("sp_fkeys", Params(("@pktable_name", d.Name)), CommandType.StoredProcedure), 
                    (r, table) => d.AddTableRow(table, new (string, string)[] { ("foreign_name", r.GetString(6)) }));
                

                // read routines where used
                FillTable(T_USED, db.GetDataReader("sp_depends", Params(("@objname ", d.Name)), CommandType.StoredProcedure), (r, table) =>
                {
                    string usedName = r.GetString(0);
                    usedName = usedName.Substring(usedName.IndexOf('.') + 1);// trim schema name
                    if (!Skip(usedName))
                        d.AddTableRow(table, new (string, string)[]
                        {
                            ("used_name", usedName),
                            ("used_type", r.GetString(1).Replace(' ', '_'))
                        });
                });
            }

            return list;
        }

        private DbObjectsList GetTablesList(DbObjectsList list)
        {
            var reader = db.GetDataReader(qTableList, null, CommandType.Text);
            if (reader.HasRows)
                while (reader.Read())
                {
                    string name = reader.GetString(1);
                    if (!Skip(name))
                        list.Add(new DbObject(name, reader.GetDateTime(3)));
                    //if (list.Count > 15) break;
                }
            reader.Close();
            return list;
        }

        private Dictionary<string, string> GetFKsList(DbObject d)
        {
            Dictionary<string, string> FKs = new Dictionary<string, string>();
            var param = new List<DbParameter>(1);
            param.Add(db.GetParameter("@fktable_name", d.Name));
            var reader = db.GetDataReader("sp_fkeys", param, CommandType.StoredProcedure);
            if (reader.HasRows)
                while (reader.Read())
                    FKs.Add(reader.GetString(7), reader.GetString(2));
            reader.Close();
            return FKs;
        }

        #endregion

        #region Routines
        public DbObjectsList GetRoutines(DbObjectsList list)
        {
            foreach (DbObject d in GetRoutinesList(list))
            {
                // init tables as empty
                foreach (string name in setRoutine)
                    d.AddTable(name);

                // read called objects
                FillTableSimple(d, T_CALL, string.Format(qRoutineReferenced, d["schema"], d.Name), r => new (string, string)[]
                    {
                        ("call_db", r.IsDBNull(0) ? "" : r.GetString(0)),
                        ("call_name", r.GetString(1)),
                        ("call_update", r.GetInt32(2) > 0 ? markup.MakeImageLink("updated") : "")
                    });
                d["callerN"] = d.GetTableValues(T_CALL).Count.ToString();

                // read callers
                FillTableSimple(d, T_CALLEE, string.Format(qRoutineReferencing, d["schema"], d.Name),
                    r => new (string, string)[] { ("callee_name", r.GetString(0)) });
                d["calleeN"] = d.GetTableValues(T_CALLEE).Count.ToString();


                // get parameters
                FillTable(T_PARAMS, string.Format(qRoutineParams, d["schema"], d.Name), (r, table) =>
                {
                    bool isOutput = r.GetBoolean(4);
                    if (isOutput) // is scalar function result row
                        d["scalarResult"] = r.GetString(2);
                    else
                        d.AddTableRow(T_PARAMS, new (string, string)[]
                        {
                            ("param_id", r.GetInt32(0).ToString()),
                            ("param_name", r.GetString(1)),
                            ("param_type", r.GetString(2))
                        });
                });

                // get results
                if (!d.ContainsKey("scalarResult")) // not scalar function
                {
                    d["scalarResult"] = "";
                    DbDataReader results = GetRoutineResultsReader(d);
                    if (results != null && results.HasRows)
                        FillTable(T_RESULT, results, (r, table) =>
                        {
                            if (r.IsDBNull(2))
                                d["scalarResult"] = "нет";
                            else
                                d.AddTableRow(table, new (string, string)[]
                                {
                                    ("result_order", r.GetInt32(1).ToString()),
                                    ("result_name", r.GetString(2)),
                                    ("result_type", r.GetString(5))
                                });
                        });
                    else
                        d["scalarResult"] = "нет";
                }

            }

            return list;
        }

        private DbObjectsList GetRoutinesList(DbObjectsList list)
        {
            var reader = db.GetDataReader(qRoutinesList, null, CommandType.Text);
            if (reader.HasRows)
                while (reader.Read())
                {
                    string name = reader.GetString(1);
                    if (!Skip(name))
                    {
                        DbObject d = new DbObject(reader.GetString(1), reader.GetDateTime(6));
                        d["type"] = reader.GetString(2);
                        d["return"] = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        d["data_access"] = reader.GetString(5) == "MODIFIES" ? markup.MakeImageLink("updated") : "";
                        d["schema"] = reader.GetString(7);
                        d["loc10"] = reader.IsDBNull(8) ? "?" : reader.GetInt32(8).ToString();

                        if (d["type"] == "FUNCTION")
                        {
                            if (d["return"] == "TABLE")
                            {
                                d["type_image"] = markup.MakeImageLink("table_function");
                                d["type_rus"] = "Табличная функция";
                            }
                            else
                            {
                                d["type_image"] = markup.MakeImageLink("scalar_function");
                                d["type_rus"] = "Скалярная функция";
                            }
                        }
                        else
                        {
                            d["type_image"] = markup.MakeImageLink("stored_procedure");
                            d["type_rus"] = "Хранимая процедура";
                        }

                        list.Add(d);
                    }
                    //if (list.Count > 15) break;
                }
            reader.Close();
            return list;
        }

        private DbDataReader GetRoutineResultsReader(DbObject d)
        {
            DbDataReader reader;
            try
            {
                if (d["type"] == "PROCEDURE")
                {
                    reader = db.GetDataReader(string.Format(qRoutineFuncResult, d.Name), null, CommandType.Text);
                }
                else
                {
                    string sql = string.Format("SELECT * FROM {0}({1})", d.Name,
                        string.Join(',', System.Linq.Enumerable.Repeat("null", d.GetTableValues("params").Count)));
                    reader = db.GetDataReader("sp_describe_first_result_set", 
                        Params(("@tsql", sql), ("@params", null), ("@browse_information_mode", 0)), CommandType.StoredProcedure);
                }
                return reader;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 7202 && ex.Class == 11)
                    return null;// known exception in case unknown server used in routine
                else if (ex.Number == 4502 && ex.Class == 16)
                    return null;// error during routine compiling
                else
                    throw;
            }
        }
        #endregion

        #region utilites
        private List<DbParameter> Params(params (string, object)[] parameters)
        {
            List<DbParameter> param = new List<DbParameter>(parameters.Length);
            foreach ((string name, object value) in parameters)
                param.Add(db.GetParameter(name, value));
            return param;
        }

        private void FillTable(string tableName, string sqlQuery, Action<DbDataReader, string> eachRow)
        {
            DbDataReader reader = db.GetDataReader(sqlQuery, null, CommandType.Text);
            if (reader.HasRows)
                while (reader.Read())
                    eachRow(reader, tableName);
            reader.Close();
        }

        private void FillTable(string tableName, DbDataReader reader, Action<DbDataReader, string> eachRow)
        {
            if (reader.HasRows)
                while (reader.Read())
                    eachRow(reader, tableName);
            reader.Close();
        }

        private void FillTableSimple(DbObject d, string tableName, string sqlQuery, 
            Func<DbDataReader, IEnumerable<(string,string)>> eachRow)
        {
            DbDataReader reader = db.GetDataReader(sqlQuery, null, CommandType.Text);
            if (reader.HasRows)
                while (reader.Read())
                    d.AddTableRow(tableName, eachRow(reader));
            reader.Close();
        }
        #endregion 
    }
}
