using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace XDM.Core.DataAccess
{
    public static class SchemaInitializer
    {
        private static void CreateTablesIfNotExists(SQLiteConnection c)
        {
            var query = @"CREATE TABLE IF NOT EXISTS downloads(
                                            id TEXT PRIMARY KEY,
                                            completed INT,
                                            name TEXT,
                                            date_added INT,
                                            size INT,
                                            status INT,
                                            progress INT,
                                            download_type TEXT,
                                            filenamefetchmode INT,
                                            maxspeedlimitinkib INT,
                                            targetdir TEXT,
                                            primary_url TEXT,
                                            referer_url TEXT,
                                            auth INT,
                                            user TEXT,
                                            pass TEXT,
                                            proxy INT,
                                            proxy_host TEXT,
                                            proxy_port INT,
                                            proxy_user TEXT,
                                            proxy_pass TEXT,
                                            proxy_type INT,
                                            error_code INT DEFAULT 0,
                                            error_message TEXT
                                        ) WITHOUT ROWID";
            using var cmd = new SQLiteCommand(c);
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();
        }

        public static void Init(SQLiteConnection c)
        {
            CreateTablesIfNotExists(c);
            EnsureErrorColumns(c);
        }

        // Adds failure columns to databases created before failure details were persisted
        private static void EnsureErrorColumns(SQLiteConnection c)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SQLiteCommand("PRAGMA table_info(downloads)", c))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    existing.Add(reader.GetString(1));
                }
            }

            // error_code INT DEFAULT 0, error_message TEXT — appended after proxy_type
            foreach (var definition in new[] { "error_code INT DEFAULT 0", "error_message TEXT" })
            {
                var columnName = definition.Split(' ')[0];
                if (existing.Contains(columnName)) continue;
                using var cmd = new SQLiteCommand($"ALTER TABLE downloads ADD COLUMN {definition}", c);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
