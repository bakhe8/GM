using System;
using System.IO;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public sealed record StagedWorkflowResponseDocument(
        string OriginalFileName,
        string SavedFileName,
        string StagingPath,
        string FinalPath);

    public class WorkflowResponseStorageService
    {
        public const long MaxResponseDocumentSizeBytes = AttachmentStorageService.MaxAttachmentFileSizeBytes;

        public StagedWorkflowResponseDocument StageCopy(string sourcePath)
        {
            AppPaths.EnsureDirectoriesExist();

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new InvalidOperationException("مسار مستند رد البنك فارغ.");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("مستند رد البنك غير موجود.", sourcePath);
            }

            long length = new FileInfo(sourcePath).Length;
            if (length > MaxResponseDocumentSizeBytes)
            {
                throw new InvalidOperationException(
                    $"حجم مستند رد البنك {Path.GetFileName(sourcePath)} يتجاوز الحد الأقصى المسموح به وهو 25 ميجابايت.");
            }

            string originalFileName = Path.GetFileName(sourcePath);
            string savedFileName = FileHelper.GenerateUniqueFileName(Path.GetExtension(sourcePath));
            string stagingPath = Path.Combine(AppPaths.WorkflowResponseStagingFolder, savedFileName);
            string finalPath = Path.Combine(AppPaths.WorkflowResponsesFolder, savedFileName);

            File.Copy(sourcePath, stagingPath, true);
            return new StagedWorkflowResponseDocument(originalFileName, savedFileName, stagingPath, finalPath);
        }

        public void FinalizeStagedCopy(StagedWorkflowResponseDocument stagedDocument, string operationName)
        {
            try
            {
                PromoteStagedCopy(stagedDocument);
            }
            catch (Exception ex)
            {
                PendingFileOperationQueue.RecordWorkflowResponsePromotionFailure(stagedDocument, ex);
                SimpleLogger.LogError(ex, $"{operationName} Workflow Response Finalization");
                throw new DeferredFilePromotionException(operationName, new[] { stagedDocument.SavedFileName }, ex);
            }
        }

        public void CleanupStagedCopy(StagedWorkflowResponseDocument? stagedDocument)
        {
            if (stagedDocument == null)
            {
                return;
            }

            try
            {
                if (File.Exists(stagedDocument.StagingPath))
                {
                    File.Delete(stagedDocument.StagingPath);
                }
            }
            catch (Exception ex)
            {
                PendingFileOperationQueue.RecordCleanupFailure(
                    stagedDocument.SavedFileName,
                    stagedDocument.StagingPath,
                    ex);
                SimpleLogger.Log(
                    $"Warning: Cleanup failed for staged workflow response {stagedDocument.SavedFileName}: {ex.Message}",
                    "WARNING");
            }
        }

        public void DeleteCommittedFile(string savedFileName)
        {
            if (string.IsNullOrWhiteSpace(savedFileName))
            {
                return;
            }

            string finalPath = Path.Combine(AppPaths.WorkflowResponsesFolder, savedFileName);

            try
            {
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
            }
            catch (Exception ex)
            {
                PendingFileOperationQueue.RecordCleanupFailure(savedFileName, finalPath, ex);
                SimpleLogger.Log(
                    $"Warning: Cleanup failed for workflow response {savedFileName}: {ex.Message}",
                    "WARNING");
            }
        }

        public void RecoverStagedFiles(SqliteConnection connection)
        {
            if (!Directory.Exists(AppPaths.WorkflowResponseStagingFolder))
            {
                return;
            }

            foreach (string stagedPath in Directory.GetFiles(AppPaths.WorkflowResponseStagingFolder))
            {
                string savedFileName = Path.GetFileName(stagedPath);
                string finalPath = Path.Combine(AppPaths.WorkflowResponsesFolder, savedFileName);

                try
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(1) FROM WorkflowRequests WHERE ResponseSavedFileName = $savedFileName";
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
                    SimpleLogger.Log($"Recovered staged workflow response file {savedFileName}.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, $"RecoverStagedWorkflowResponseFiles ({savedFileName})");
                }
            }
        }

        private static void PromoteStagedCopy(StagedWorkflowResponseDocument stagedDocument)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stagedDocument.FinalPath)!);

            if (File.Exists(stagedDocument.FinalPath))
            {
                if (File.Exists(stagedDocument.StagingPath))
                {
                    File.Delete(stagedDocument.StagingPath);
                }

                return;
            }

            if (!File.Exists(stagedDocument.StagingPath))
            {
                throw new FileNotFoundException("ملف رد البنك المرحلي غير موجود.", stagedDocument.StagingPath);
            }

            File.Move(stagedDocument.StagingPath, stagedDocument.FinalPath);
        }
    }
}
