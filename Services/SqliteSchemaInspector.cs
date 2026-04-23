using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Services
{
    internal static class SqliteSchemaInspector
    {
        public static bool TableExists(SqliteConnection connection, string tableName)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
            cmd.Parameters.AddWithValue("$name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        public static HashSet<string> GetTableColumns(SqliteConnection connection, string tableName, SqliteTransaction? transaction = null)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA table_info('{tableName}')";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }

        public static string? GetIndexSql(SqliteConnection connection, string indexName, SqliteTransaction? transaction = null)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = $name";
            command.Parameters.AddWithValue("$name", indexName);
            return command.ExecuteScalar() as string;
        }
    }
}
