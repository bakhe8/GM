using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public sealed record StagedAttachmentFile(
        string OriginalFileName,
        string SavedFileName,
        string FileExtension,
        string StagingPath,
        string FinalPath);

    public class AttachmentStorageService
    {
        public List<StagedAttachmentFile> StageCopies(IEnumerable<string> sourcePaths)
        {
            AppPaths.EnsureDirectoriesExist();
            var stagedCopies = new List<StagedAttachmentFile>();

            try
            {
                foreach (string sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
                {
                    string originalFileName = Path.GetFileName(sourcePath);
                    string fileExtension = Path.GetExtension(sourcePath);
                    string savedFileName = FileHelper.GenerateUniqueFileName(fileExtension);
                    string stagingPath = Path.Combine(AppPaths.AttachmentStagingFolder, savedFileName);
                    string finalPath = Path.Combine(AppPaths.AttachmentsFolder, savedFileName);

                    File.Copy(sourcePath, stagingPath, true);
                    stagedCopies.Add(new StagedAttachmentFile(
                        originalFileName,
                        savedFileName,
                        fileExtension,
                        stagingPath,
                        finalPath));
                }

                return stagedCopies;
            }
            catch
            {
                CleanupStagedCopies(stagedCopies);
                throw;
            }
        }

        public void CleanupStagedCopies(IEnumerable<StagedAttachmentFile> stagedCopies)
        {
            foreach (var stagedCopy in stagedCopies)
            {
                try
                {
                    if (File.Exists(stagedCopy.StagingPath))
                    {
                        File.Delete(stagedCopy.StagingPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    SimpleLogger.Log(
                        $"Warning: Cleanup failed for staged attachment {stagedCopy.SavedFileName}: {cleanupEx.Message}",
                        "WARNING");
                }
            }
        }

        public void FinalizeStagedCopies(IEnumerable<StagedAttachmentFile> stagedCopies, string operationName)
        {
            var failures = new List<(string SavedFileName, Exception Error)>();

            foreach (var stagedCopy in stagedCopies)
            {
                try
                {
                    PromoteStagedCopy(stagedCopy);
                }
                catch (Exception ex)
                {
                    failures.Add((stagedCopy.SavedFileName, ex));
                }
            }

            if (failures.Count == 0)
            {
                return;
            }

            var aggregateException = new AggregateException(
                $"Attachment promotion deferred after {operationName}.",
                failures.Select(failure => failure.Error));
            SimpleLogger.LogError(aggregateException, $"{operationName} Attachment Finalization");
            throw new DeferredFilePromotionException(
                operationName,
                failures.Select(failure => failure.SavedFileName),
                aggregateException);
        }

        public void RecoverStagedFiles(SqliteConnection connection)
        {
            if (!Directory.Exists(AppPaths.AttachmentStagingFolder))
            {
                return;
            }

            foreach (string stagedPath in Directory.GetFiles(AppPaths.AttachmentStagingFolder))
            {
                string savedFileName = Path.GetFileName(stagedPath);
                string finalPath = Path.Combine(AppPaths.AttachmentsFolder, savedFileName);

                try
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(1) FROM Attachments WHERE SavedFileName = $savedFileName";
                    command.Parameters.AddWithValue("$savedFileName", savedFileName);
                    long referenceCount = Convert.ToInt64(command.ExecuteScalar());

                    if (referenceCount == 0)
                    {
                        File.Delete(stagedPath);
                        continue;
                    }

                    if (File.Exists(finalPath))
                    {
                        File.Delete(stagedPath);
                        continue;
                    }

                    File.Move(stagedPath, finalPath);
                    SimpleLogger.Log($"Recovered staged attachment file {savedFileName}.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, $"RecoverStagedAttachmentFiles ({savedFileName})");
                }
            }
        }

        public void DeletePhysicalFileIfUnreferenced(string savedFileName, SqliteConnection connection)
        {
            if (IsFileReferencedElsewhere(savedFileName, connection))
            {
                return;
            }

            string filePath = Path.Combine(AppPaths.AttachmentsFolder, savedFileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Warning: Cleanup failed for {filePath}: {ex.Message}", "WARNING");
            }
        }

        private static void PromoteStagedCopy(StagedAttachmentFile stagedCopy)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stagedCopy.FinalPath)!);

            if (File.Exists(stagedCopy.FinalPath))
            {
                if (File.Exists(stagedCopy.StagingPath))
                {
                    File.Delete(stagedCopy.StagingPath);
                }

                return;
            }

            if (!File.Exists(stagedCopy.StagingPath))
            {
                throw new FileNotFoundException("ملف المرفق المرحلي غير موجود.", stagedCopy.StagingPath);
            }

            File.Move(stagedCopy.StagingPath, stagedCopy.FinalPath);
        }

        private static bool IsFileReferencedElsewhere(string savedFileName, SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Attachments WHERE SavedFileName = $name";
            cmd.Parameters.AddWithValue("$name", savedFileName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}
