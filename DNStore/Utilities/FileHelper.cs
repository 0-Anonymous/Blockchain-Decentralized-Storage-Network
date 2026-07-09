using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;

namespace DeezFiles.Utilities
{
    public class FileHelper
    {
        public static async Task UploadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("File path is invalid!");
                return;
            }

            bool uploadSucceeded = await LocalFileHelper.CreateChunks(filePath);
            if (!uploadSucceeded)
            {
                MessageBox.Show(
                    "Upload did not finish, so the file was not added for download.\nCheck that at least one storage node is online and reachable.",
                    "Upload Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LocalFileHelper.SaveUPFileDetails(filePath);
            MessageBox.Show(
                "Upload completed successfully.",
                "Upload Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public static async Task DownloadFile(string filename)
        {
            string filehash = await LocalFileHelper.RetrieveHash(filename);
            if (string.IsNullOrEmpty(filehash))
            {
                Console.WriteLine($"[FileHelper] Could not find hash for file: {filename}");
                MessageBox.Show(
                    $"The file '{filename}' is missing its hash metadata.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // The .dn file contains the ordered list of shard hashes.
            string recreationInfoPath = Path.Combine(LocalFileHelper.uploadqueuePath, $"{filehash}.dn");
            if (!File.Exists(recreationInfoPath))
            {
                string legacyUploadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Uploads",
                    $"{filehash}.dn");
                recreationInfoPath = legacyUploadPath;
            }

            if (!File.Exists(recreationInfoPath))
            {
                Console.WriteLine($"[FileHelper] Recreation info file not found for hash: {filehash}");
                MessageBox.Show(
                    $"The file '{filename}' was listed, but its shard map (.dn) was never created.\nThis usually means the upload did not finish successfully.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string[] shardHashes = (await File.ReadAllTextAsync(recreationInfoPath)).Split(';');

            // Hand off the download job to the orchestration service.
            FileDownloader.InitiateDownload(filename, shardHashes);
        }

        public static async Task DeleteFile(string filename)
        {
            bool deleted = await LocalFileHelper.DeleteUploadedFileAsync(filename);
            if (!deleted)
            {
                MessageBox.Show(
                    $"The file '{filename}' could not be deleted safely.",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                $"'{filename}' was removed from your uploads and its local shards were cleaned up.",
                "File Deleted",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }



    }
}
