using System;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Tests
{
    public abstract class DatabaseWorkflowTestBase
    {
        protected readonly TestEnvironmentFixture _fixture;

        protected DatabaseWorkflowTestBase(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        protected static long CountRows(string tableName)
        {
            string safeTableName = tableName switch
            {
                "Guarantees" => "Guarantees",
                "WorkflowRequests" => "WorkflowRequests",
                "Attachments" => "Attachments",
                _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unsupported table.")
            };

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {safeTableName}";
            return Convert.ToInt64(command.ExecuteScalar());
        }

        protected static string QueryFirstGuaranteeNo()
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT GuaranteeNo FROM Guarantees ORDER BY Id LIMIT 1";
            return Convert.ToString(command.ExecuteScalar())
                ?? throw new InvalidOperationException("No seeded guarantee was found.");
        }

        protected static long GetSchemaObjectCount(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table', 'index', 'view', 'trigger')";
            return Convert.ToInt64(command.ExecuteScalar());
        }

        protected static void OpenWithoutEncryption(string databasePath)
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(
                databasePath,
                SqliteOpenMode.ReadWrite,
                encrypted: false,
                pooling: false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            _ = command.ExecuteScalar();
        }
    }
}
