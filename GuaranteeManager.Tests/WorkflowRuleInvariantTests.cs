using System;
using System.Collections.Generic;
#if DEBUG
using GuaranteeManager.Development;
#endif
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class WorkflowRuleInvariantTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public WorkflowRuleInvariantTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

#if DEBUG
        [Fact]
        public void DataSeedingService_GeneratedData_RespectsFinalizedWorkflowRules()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            string seedStorageRoot = _fixture.CreateStorageRoot($"seed-invariants-{_fixture.NextToken("ROOT")}");

            try
            {
                _fixture.SwitchStorageRoot(seedStorageRoot);
                DatabaseService.InitializeRuntime();

                DatabaseService database = _fixture.CreateDatabaseService();
                WorkflowService workflow = _fixture.CreateWorkflowService(database);
                var seeding = new DataSeedingService(database, workflow);

                seeding.Seed(clearExistingData: true);
                DatabaseService.ResetRuntimeInitializationForTesting();
                DatabaseService.InitializeRuntime();

                List<string> violations = LoadWorkflowInvariantViolations();

                Assert.True(
                    violations.Count == 0,
                    "Generated seed data violates workflow rules:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, violations));
            }
            finally
            {
                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }
#endif

        private static List<string> LoadWorkflowInvariantViolations()
        {
            var violations = new List<string>();

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);

            AddViolations(
                connection,
                violations,
                "Each guarantee root must have exactly one current row.",
                @"
                    SELECT RootKey, CurrentCount
                    FROM (
                        SELECT COALESCE(RootId, Id) AS RootKey, COUNT(*) AS CurrentCount
                        FROM Guarantees
                        WHERE IsCurrent = 1
                        GROUP BY COALESCE(RootId, Id)
                    )
                    WHERE CurrentCount <> 1",
                reader => $"root={reader.GetInt32(0)}, currentRows={reader.GetInt32(1)}");

            AddViolations(
                connection,
                violations,
                "Pending requests must match the current lifecycle and expiry state.",
                @"
                    SELECT wr.Id, wr.RootId, wr.RequestType, currentG.LifecycleStatus
                    FROM WorkflowRequests wr
                    JOIN Guarantees currentG
                      ON COALESCE(currentG.RootId, currentG.Id) = wr.RootId
                     AND currentG.IsCurrent = 1
                    WHERE wr.RequestStatus = 'Pending'
                      AND NOT (
                            (wr.RequestType = 'Release' AND currentG.LifecycleStatus IN ('Active', 'Expired'))
                         OR (wr.RequestType <> 'Release' AND currentG.LifecycleStatus = 'Active' AND date(currentG.ExpiryDate) >= date('now'))
                      )",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, type={reader.GetString(2)}, lifecycle={reader.GetString(3)}");

            AddViolations(
                connection,
                violations,
                "Generated data must not create annulment requests.",
                @"
                    SELECT Id, RootId, RequestStatus
                    FROM WorkflowRequests
                    WHERE RequestType = 'Annulment'",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, status={reader.GetString(2)}");

            AddViolations(
                connection,
                violations,
                "Generated guarantees must use the single program beneficiary.",
                @"
                    SELECT Id, GuaranteeNo, Beneficiary
                    FROM Guarantees
                    WHERE TRIM(IFNULL(Beneficiary, '')) <> 'مستشفى الملك فيصل التخصصي ومركز الأبحاث'",
                reader => $"guarantee={reader.GetInt32(0)}, no={reader.GetString(1)}, beneficiary={ReadNullableString(reader, 2)}");

            AddViolations(
                connection,
                violations,
                "A root cannot have duplicate pending requests of the same type.",
                @"
                    SELECT RootId, RequestType, COUNT(*)
                    FROM WorkflowRequests
                    WHERE RequestStatus = 'Pending'
                    GROUP BY RootId, RequestType
                    HAVING COUNT(*) > 1",
                reader => $"root={reader.GetInt32(0)}, type={reader.GetString(1)}, pendingCount={reader.GetInt32(2)}");

            AddViolations(
                connection,
                violations,
                "Lifecycle-ending executions must not point to a result version.",
                @"
                    SELECT Id, RootId, RequestType, ResultVersionId
                    FROM WorkflowRequests
                    WHERE RequestStatus = 'Executed'
                      AND RequestType IN ('Release', 'Liquidation')
                      AND ResultVersionId IS NOT NULL",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, type={reader.GetString(2)}, resultVersion={reader.GetInt32(3)}");

            AddViolations(
                connection,
                violations,
                "Extension and reduction executions must create a result version on the same root.",
                @"
                    SELECT wr.Id, wr.RootId, wr.RequestType, wr.ResultVersionId
                    FROM WorkflowRequests wr
                    LEFT JOIN Guarantees resultG ON resultG.Id = wr.ResultVersionId
                    WHERE wr.RequestStatus = 'Executed'
                      AND wr.RequestType IN ('Extension', 'Reduction')
                      AND (
                            wr.ResultVersionId IS NULL
                         OR COALESCE(resultG.RootId, resultG.Id) <> wr.RootId
                      )",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, type={reader.GetString(2)}, resultVersion={ReadNullableInt(reader, 3)}");

            AddViolations(
                connection,
                violations,
                "Replacement executions must create a replacement guarantee linked to the old root.",
                @"
                    SELECT wr.Id, wr.RootId, wr.ResultVersionId, resultG.ReplacesRootId
                    FROM WorkflowRequests wr
                    LEFT JOIN Guarantees resultG ON resultG.Id = wr.ResultVersionId
                    WHERE wr.RequestStatus = 'Executed'
                      AND wr.RequestType = 'Replacement'
                      AND (
                            wr.ResultVersionId IS NULL
                         OR resultG.ReplacesRootId <> wr.RootId
                      )",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, resultVersion={ReadNullableInt(reader, 2)}, replacesRoot={ReadNullableInt(reader, 3)}");

            AddViolations(
                connection,
                violations,
                "Executed replacements must leave the original current row marked as Replaced.",
                @"
                    SELECT wr.Id, wr.RootId, currentG.Id, currentG.LifecycleStatus
                    FROM WorkflowRequests wr
                    JOIN Guarantees currentG
                      ON COALESCE(currentG.RootId, currentG.Id) = wr.RootId
                     AND currentG.IsCurrent = 1
                    WHERE wr.RequestStatus = 'Executed'
                      AND wr.RequestType = 'Replacement'
                      AND currentG.LifecycleStatus <> 'Replaced'",
                reader => $"request={reader.GetInt32(0)}, root={reader.GetInt32(1)}, currentGuarantee={reader.GetInt32(2)}, lifecycle={reader.GetString(3)}");

            return violations;
        }

        private static void AddViolations(
            SqliteConnection connection,
            List<string> violations,
            string rule,
            string sql,
            Func<SqliteDataReader, string> format)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                violations.Add($"{rule} {format(reader)}");
            }
        }

        private static string ReadNullableInt(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? "null"
                : reader.GetInt32(ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ReadNullableString(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? "null"
                : reader.GetString(ordinal);
        }
    }
}
