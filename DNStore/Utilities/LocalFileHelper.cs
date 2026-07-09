using DeezFiles.Models;
using DeezFiles.Services;
using DNStore.Models;
using DNStore.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeezFiles.Utilities
{
    internal class LocalFileHelper
    {
        public static event EventHandler FileListUpdated;
        public static event Action<string> DownloadCompleted;
        public static event Action<string> DownloadFailed;

        public static string statePath;
        public static string userFolderPath;
        public static string documentsPath;
        public static string dnStorePath;
        public static string mainstoragePath;
        public static string uploadqueuePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Uploads");
        public static string configPath;
        public static string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static string initializedUsername;

        // ✅ ADD THIS BLOCK EXACTLY HERE 👇
        static LocalFileHelper()
        {
            if (!Directory.Exists(uploadqueuePath))
                Directory.CreateDirectory(uploadqueuePath);

            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);
        }

        public static void EnsureInitialized(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Username is required to initialize local storage.");

            if (!string.Equals(initializedUsername, username, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(statePath) ||
                string.IsNullOrWhiteSpace(userFolderPath) ||
                !Directory.Exists(userFolderPath))
            {
                _ = new LocalFileHelper(username);
            }
        }

        public LocalFileHelper(string username)
        {

            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dnStorePath = Path.Combine(documentsPath, "DNStore");
            userFolderPath = Path.Combine(dnStorePath, username);
            mainstoragePath = Path.Combine(userFolderPath, "storage");
            uploadqueuePath = Path.Combine(userFolderPath, "uploadData");
            configPath = Path.Combine(userFolderPath, "config");
            downloadPath = Path.Combine(userFolderPath, "downloads");
            statePath = Path.Combine(userFolderPath, "state");
            initializedUsername = username;

            // ✅ Step 2: Then create folders
            if (!Directory.Exists(dnStorePath))
                Directory.CreateDirectory(dnStorePath);

            if (!Directory.Exists(userFolderPath))
                Directory.CreateDirectory(userFolderPath);

            if (!Directory.Exists(mainstoragePath))
                Directory.CreateDirectory(mainstoragePath);

            if (!Directory.Exists(configPath))
                Directory.CreateDirectory(configPath);

            if (!Directory.Exists(statePath))
                Directory.CreateDirectory(statePath);

            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);

            if (!Directory.Exists(uploadqueuePath))
                Directory.CreateDirectory(uploadqueuePath);
        }

        public void SetupRegistrationFolders()
        {
            if (!Directory.Exists(dnStorePath)) Directory.CreateDirectory(dnStorePath);
            if (!Directory.Exists(userFolderPath)) Directory.CreateDirectory(userFolderPath);
            if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
            if (!Directory.Exists(downloadPath)) Directory.CreateDirectory(downloadPath);
            if (!Directory.Exists(mainstoragePath)) Directory.CreateDirectory(mainstoragePath);
            if (!Directory.Exists(uploadqueuePath)) Directory.CreateDirectory(uploadqueuePath);
            if (!Directory.Exists(statePath)) Directory.CreateDirectory(statePath);
        }

        public void SaveMasterKey(string mKey)
        {
            string secretfile = Path.Combine(configPath, "secret.txt");
            File.WriteAllText(secretfile, mKey);
        }

        public static void SaveDNETaddress(string username, string address)
        {
            // ✅ Ensure base folder exists
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dnStorePath = Path.Combine(documentsPath, "DNStore");
            string userFolderPath = Path.Combine(dnStorePath, username);
            string configPathLocal = Path.Combine(userFolderPath, "config");

            // ✅ Create directories if missing
            if (!Directory.Exists(dnStorePath)) Directory.CreateDirectory(dnStorePath);
            if (!Directory.Exists(userFolderPath)) Directory.CreateDirectory(userFolderPath);
            if (!Directory.Exists(configPathLocal)) Directory.CreateDirectory(configPathLocal);

            // ✅ Save file
            string addressfile = Path.Combine(configPathLocal, "add.txt");
            string data = username + ":" + address;

            File.WriteAllText(addressfile, data);
        }

        public static string GetDNETaddress(string username)
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dnStorePath = Path.Combine(documentsPath, "DNStore");
            string userFolderPath = Path.Combine(dnStorePath, username);
            string configPathLocal = Path.Combine(userFolderPath, "config");

            string addressfile = Path.Combine(configPathLocal, "add.txt");

            if (!File.Exists(addressfile))
                return null;

            return File.ReadAllText(addressfile);
        }

        public static async Task SaveUPFileDetails(string filePath)
        {
            EnsureInitialized(AuthorizationService.currentUsername);

            string fileName = Path.GetFileName(filePath);
            DateTime uploadTime = DateTime.Now;
            var fileInfo = new FileInfo(filePath);
            ulong fileSize = (ulong)fileInfo.Length;
            string sha256Hash = await CalculateSHA256(filePath);
            var fileState = new { UploadTime = uploadTime, Size = fileSize, SHA256 = sha256Hash };
            string jsonFilePath = Path.Combine(statePath, "filestate.json");
            string jsonData = string.Empty;
            if (File.Exists(jsonFilePath)) { jsonData = await File.ReadAllTextAsync(jsonFilePath); }
            var fileStateDict = string.IsNullOrEmpty(jsonData) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
            fileStateDict[fileName] = fileState;
            string updatedJson = JsonSerializer.Serialize(fileStateDict, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonFilePath, updatedJson);
            await PublishFileManifestAsync(fileName, uploadTime, fileSize, sha256Hash);
            FileListUpdated?.Invoke(null, EventArgs.Empty);
        }

        public static async Task SyncFileManifestsFromServerAsync()
        {
            EnsureInitialized(AuthorizationService.currentUsername);

            string ownerAddress = GetOwnerAddress();
            if (string.IsNullOrWhiteSpace(ownerAddress))
                return;

            try
            {
                HttpResponseMessage response = await NetworkService.SendGetRequest($"FileManifests/{Uri.EscapeDataString(ownerAddress)}");
                if (!response.IsSuccessStatusCode)
                    return;

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var manifests = JsonSerializer.Deserialize<List<FileManifestDto>>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<FileManifestDto>();

                string jsonFilePath = Path.Combine(statePath, "filestate.json");
                var fileStateDict = new Dictionary<string, object>();

                foreach (var manifest in manifests)
                {
                    if (string.IsNullOrWhiteSpace(manifest.FileName) ||
                        string.IsNullOrWhiteSpace(manifest.FileHash) ||
                        manifest.ShardHashes == null ||
                        manifest.ShardHashes.Count == 0)
                    {
                        continue;
                    }

                    fileStateDict[manifest.FileName] = new
                    {
                        manifest.UploadTime,
                        manifest.Size,
                        SHA256 = manifest.FileHash
                    };

                    string shardMapPath = Path.Combine(uploadqueuePath, $"{manifest.FileHash}.dn");
                    string shardMap = string.Join(";", manifest.ShardHashes.Where(h => !string.IsNullOrWhiteSpace(h)));
                    await File.WriteAllTextAsync(shardMapPath, shardMap);
                }

                string updatedJson = JsonSerializer.Serialize(fileStateDict, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonFilePath, updatedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalFileHelper] Could not sync file manifests: {ex.Message}");
            }
        }

        private static async Task<string> CalculateSHA256(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public static async Task<string> RetrieveHash(string filename)
        {
            EnsureInitialized(AuthorizationService.currentUsername);

            string jsonFilePath = Path.Combine(statePath, "filestate.json");
            if (!File.Exists(jsonFilePath)) { return null; }
            string jsonData = await File.ReadAllTextAsync(jsonFilePath);
            var fileStateDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData);
            if (fileStateDict != null && fileStateDict.TryGetValue(filename, out var fileStateElement))
            {
                if (fileStateElement.TryGetProperty("SHA256", out var sha256Element))
                {
                    return sha256Element.GetString();
                }
            }
            return null;
        }

        public static async Task<bool> DeleteUploadedFileAsync(string filename)
        {
            EnsureInitialized(AuthorizationService.currentUsername);

            if (string.IsNullOrWhiteSpace(filename))
                return false;

            string fileHash = await RetrieveHash(filename);
            if (string.IsNullOrWhiteSpace(fileHash))
                return false;

            string recreationInfoPath = GetRecreationInfoPath(fileHash);
            List<string> shardHashes = new List<string>();

            if (File.Exists(recreationInfoPath))
            {
                string shardList = await File.ReadAllTextAsync(recreationInfoPath);
                shardHashes = shardList
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(hash => hash.Trim())
                    .Where(hash => !string.IsNullOrWhiteSpace(hash))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                File.Delete(recreationInfoPath);
            }

            foreach (string shardHash in shardHashes)
            {
                DeleteIfExists(Path.Combine(mainstoragePath, shardHash + ".shard"));
                DeleteIfExists(Path.Combine(downloadPath, shardHash + ".shard-dl"));
            }

            DeleteIfExists(Path.Combine(downloadPath, filename));
            await RemoveFileStateEntryAsync(filename);
            await DeleteFileManifestAsync(fileHash);
            FileListUpdated?.Invoke(null, EventArgs.Empty);
            return true;
        }

        static (byte[] key, byte[] iv) LoadAESKeyIV(string filePath)
        {
            string content = File.ReadAllText(filePath);
            string[] parts = content.Split(';');
            if (parts.Length != 2) throw new Exception("Invalid key file format!");
            byte[] key = Convert.FromBase64String(parts[0]);
            byte[] iv = Convert.FromBase64String(parts[1]);
            return (key, iv);
        }

        public static async Task<bool> CreateChunks(string filePath)
        {
            EnsureInitialized(AuthorizationService.currentUsername);
            ClearPendingTempFiles();

            const int chunkSize = 256 * 1024;
            string hashFilePath;
            string originalFileHash;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sha256 = SHA256.Create()) { originalFileHash = BitConverter.ToString(sha256.ComputeHash(fs)).Replace("-", "").ToLowerInvariant(); }
                fs.Position = 0;
                byte[] buffer = new byte[chunkSize];
                int bytesRead;
                int partNumber = 1;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, chunkSize)) > 0)
                {
                    string tempFileName = $"temp{partNumber}";
                    string tempFilePath = Path.Combine(uploadqueuePath, tempFileName);
                    byte[] actualData = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, actualData, 0, bytesRead);
                    await File.WriteAllBytesAsync(tempFilePath, actualData);
                    partNumber++;
                }
                hashFilePath = Path.Combine(uploadqueuePath, $"{originalFileHash}.dn");
            }
            List<byte[]> chunkHashList = await StoreChunks();
            if (chunkHashList != null && chunkHashList.Any())
            {
                StoreRecreationInfo(chunkHashList, hashFilePath);
                return true;
            }

            return false;
        }

        private static void ClearPendingTempFiles()
        {
            if (!Directory.Exists(uploadqueuePath))
                return;

            foreach (var tempFile in Directory.GetFiles(uploadqueuePath, "temp*"))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocalFileHelper] Failed to clear temp file '{tempFile}': {ex.Message}");
                }
            }
        }

        private static async Task<List<byte[]>> StoreChunks()
        {
            List<byte[]> chunksHash = new List<byte[]>();
            var tempFiles = Directory.GetFiles(uploadqueuePath).Where(f => Path.GetFileName(f).StartsWith("temp")).OrderBy(f => f).ToList();
            foreach (var file in tempFiles)
            {
                bool shardSentSuccessfully = false;
                try
                {
                    string secretPath = Path.Combine(configPath, "secret.txt");
                    var (key, iv) = LoadAESKeyIV(secretPath);
                    byte[] data = await File.ReadAllBytesAsync(file);
                    byte[] encryptedData = CryptHelper.EncryptData(data, key, iv);
                    string shardHash = CalculateShardHash(encryptedData);

                    OnlineNode selectedNode = await Blockchain.SelectBestNode();
                    if (selectedNode == null)
                    {
                        Console.WriteLine("No remote nodes available. Saving shard locally.");
                        await SaveAndCreateShardTransactionAsync(encryptedData);
                        chunksHash.Add(HexStringToBytes(shardHash));
                        shardSentSuccessfully = true;
                        continue;
                    }

                    Console.WriteLine($"Attempting to establish connection with {selectedNode.dnAddress}...");
                    await Blockchain.p2pService.PunchPeers(new List<OnlineNode> { selectedNode });
                    bool isReady = await Blockchain.p2pService.CheckReadinessAsync(selectedNode);

                    await Task.Delay(500);
                    if (!isReady)
                    {
                        Console.WriteLine($"Peer {selectedNode.dnAddress} did not confirm readiness. Attempting Azure relay...");
                        bool relaySuccess = await NetworkService.UploadShardToRelay(shardHash, encryptedData);
                        if (relaySuccess)
                        {
                            Console.WriteLine($"Shard {shardHash} uploaded to Azure relay successfully.");
                            chunksHash.Add(HexStringToBytes(shardHash));
                            shardSentSuccessfully = true;
                            continue;
                        }
                        Console.WriteLine($"Azure relay also failed. Saving shard locally.");
                        await SaveAndCreateShardTransactionAsync(encryptedData);
                        chunksHash.Add(HexStringToBytes(shardHash));
                        shardSentSuccessfully = true;
                        continue;
                    }

                    Console.WriteLine($"Connection established with {selectedNode.dnAddress}. Encrypting and sending shard...");
                    bool success = await Blockchain.p2pService.SendDataAsync(selectedNode.ipAddress, selectedNode.port, "SAVESHARD", encryptedData);
                    if (success)
                    {
                        Console.WriteLine("Shard sent successfully.");
                        chunksHash.Add(HexStringToBytes(shardHash));
                        shardSentSuccessfully = true;
                        await Task.Delay(1000);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to send shard to {selectedNode.dnAddress}. Attempting Azure relay...");
                        bool relaySuccess = await NetworkService.UploadShardToRelay(shardHash, encryptedData);
                        if (relaySuccess)
                        {
                            Console.WriteLine($"Shard {shardHash} uploaded to Azure relay successfully.");
                            chunksHash.Add(HexStringToBytes(shardHash));
                            shardSentSuccessfully = true;
                        }
                        else
                        {
                            Console.WriteLine($"Azure relay also failed. Saving shard locally.");
                            await SaveAndCreateShardTransactionAsync(encryptedData);
                            chunksHash.Add(HexStringToBytes(shardHash));
                            shardSentSuccessfully = true;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"An error occurred during shard processing: {ex.Message}"); return null; }
                finally { if (shardSentSuccessfully) { File.Delete(file); } }
            }
            return chunksHash;
        }

        private static void StoreRecreationInfo(List<byte[]> hashes, string hashFilepath)
        {
            try
            {
                string hashesJoined = string.Join(";", hashes.Select(h => BitConverter.ToString(h).Replace("-", "").ToLowerInvariant()));
                File.WriteAllText(hashFilepath, hashesJoined);
                Console.WriteLine($"Recreation info for {Path.GetFileName(hashFilepath)} written successfully.");
            }
            catch (Exception ex) { Console.WriteLine($"Error writing recreation info: {ex.Message}"); }
        }

        private static async Task PublishFileManifestAsync(string fileName, DateTime uploadTime, ulong fileSize, string fileHash)
        {
            try
            {
                string recreationInfoPath = GetRecreationInfoPath(fileHash);
                if (!File.Exists(recreationInfoPath))
                    return;

                string shardList = await File.ReadAllTextAsync(recreationInfoPath);
                var shardHashes = shardList
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(hash => hash.Trim())
                    .Where(hash => !string.IsNullOrWhiteSpace(hash))
                    .ToList();

                if (shardHashes.Count == 0)
                    return;

                var manifest = new FileManifestDto
                {
                    OwnerAddress = GetOwnerAddress(),
                    FileName = fileName,
                    FileHash = fileHash,
                    Size = fileSize,
                    UploadTime = uploadTime,
                    ShardHashes = shardHashes
                };

                string json = JsonSerializer.Serialize(manifest);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await NetworkService.SendPostRequest("FileManifests", content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[LocalFileHelper] Manifest publish failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalFileHelper] Could not publish file manifest: {ex.Message}");
            }
        }

        private static async Task DeleteFileManifestAsync(string fileHash)
        {
            try
            {
                string ownerAddress = GetOwnerAddress();
                if (string.IsNullOrWhiteSpace(ownerAddress) || string.IsNullOrWhiteSpace(fileHash))
                    return;

                await NetworkService.SendDeleteRequest(
                    $"FileManifests/{Uri.EscapeDataString(ownerAddress)}/{Uri.EscapeDataString(fileHash)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalFileHelper] Could not delete file manifest: {ex.Message}");
            }
        }

        private static string GetOwnerAddress()
        {
            if (!string.IsNullOrWhiteSpace(AuthorizationService.accountAddress))
                return AuthorizationService.accountAddress;

            if (!string.IsNullOrWhiteSpace(AuthorizationService.nodeAddress))
                return AuthorizationService.nodeAddress.Split(':')[0];

            return null;
        }

        private class FileManifestDto
        {
            public string OwnerAddress { get; set; }
            public string FileName { get; set; }
            public string FileHash { get; set; }
            public ulong Size { get; set; }
            public DateTime UploadTime { get; set; }
            public List<string> ShardHashes { get; set; } = new List<string>();
        }

        public static async void SaveAndCreateShardTransaction(byte[] shardData)
        {
            await SaveAndCreateShardTransactionAsync(shardData);
        }

        public static async Task SaveAndCreateShardTransactionAsync(byte[] shardData)
        {
            try
            {
                string shardName = CalculateShardHash(shardData);
                string shardPath = Path.Combine(mainstoragePath, shardName + ".shard");
                await File.WriteAllBytesAsync(shardPath, shardData);
                Console.WriteLine($"Shard {shardName} saved to local storage.");
                var transaction = new StorageCommitmentTransaction { NodeId = AuthorizationService.nodeAddress, Timestamp = DateTime.UtcNow, ChunkHash = shardName, TransactionType = "STORAGE" };
                await Blockchain.AddTransaction(transaction);
                string transitTransaction = Newtonsoft.Json.JsonConvert.SerializeObject(transaction);
                List<OnlineNode> onlineNodes = await Blockchain.GetOnlineNodes();
                if (onlineNodes.Any())
                {
                    await Blockchain.p2pService.PunchPeers(onlineNodes);
                    foreach (OnlineNode onlineNode in onlineNodes) { await Blockchain.p2pService.SendMessageAsync(onlineNode.ipAddress, onlineNode.port, "ADDTRANSACTION", transitTransaction); }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error saving shard and creating transaction: {ex.Message}"); }
        }

        public static byte[] RetrieveShards(string hash)
        {
            string shardPath = Path.Combine(mainstoragePath, hash + ".shard");
            if (File.Exists(shardPath)) { return File.ReadAllBytes(shardPath); }
            return null;
        }

        public static async Task SaveDownloadedShardAsync(byte[] shardData)
        {
            if (shardData == null || shardData.Length == 0) { Console.WriteLine("[LocalFileHelper] Received an empty shard. Skipping save."); return; }
            try
            {
                Directory.CreateDirectory(downloadPath);

                string shardName;
                using (SHA256 sha256 = SHA256.Create()) { shardName = BitConverter.ToString(sha256.ComputeHash(shardData)).Replace("-", "").ToLowerInvariant(); }
                string shardPath = Path.Combine(downloadPath, shardName + ".shard-dl");
                await File.WriteAllBytesAsync(shardPath, shardData);
                Console.WriteLine($"[LocalFileHelper] Downloaded shard saved: {shardName}");
                bool shardWasExpected = FileDownloader.OnShardDownloaded(shardName, shardPath);
                if (!shardWasExpected)
                {
                    DeleteIfExists(shardPath);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[LocalFileHelper] Error saving downloaded shard: {ex.Message}"); }
        }

        public static async Task FindAndDownloadShardAsync(string shardHash)
        {
            if (string.IsNullOrWhiteSpace(shardHash))
                return;

            var localShard = RetrieveShards(shardHash);
            if (localShard != null)
            {
                Console.WriteLine($"Shard {shardHash} found locally. Using local copy for download.");
                await SaveDownloadedShardAsync(localShard);
                return;
            }

            var blockchain = await Blockchain.GetBlockchain();
            var transaction = blockchain.SelectMany(b => b.Transactions).FirstOrDefault(t => t.ChunkHash.Equals(shardHash, StringComparison.OrdinalIgnoreCase));
            if (transaction == null)
            {
                Console.WriteLine($"Shard hash {shardHash} not found on the local blockchain. Asking online peers.");
                await RequestShardFromAllOnlineNodesAsync(shardHash);
                return;
            }
            if (transaction.NodeId == AuthorizationService.nodeAddress)
            {
                var ownShard = RetrieveShards(shardHash);
                if (ownShard != null)
                {
                    Console.WriteLine($"Shard {shardHash} belongs to this node. Using local copy for download.");
                    await SaveDownloadedShardAsync(ownShard);
                    return;
                }
            }
            var onlineNodes = await Blockchain.GetOnlineNodes();
            var holderNode = onlineNodes.FirstOrDefault(n => n.dnAddress == transaction.NodeId);
            if (holderNode == null)
            {
                Console.WriteLine($"Node {transaction.NodeId} is not online. Asking all online peers for shard {shardHash}.");
                await RequestShardFromAllOnlineNodesAsync(shardHash);
                return;
            }

            bool requested = await RequestShardFromNodeAsync(holderNode, shardHash);
            if (!requested)
            {
                Console.WriteLine($"Could not reach recorded holder {holderNode.dnAddress}. Asking all online peers for shard {shardHash}.");
                await RequestShardFromAllOnlineNodesAsync(shardHash);
                byte[] relayData = await NetworkService.DownloadShardFromRelay(shardHash);
                if (relayData != null)
                {
                    Console.WriteLine($"[Relay] Shard {shardHash} recovered from Azure relay as last resort.");
                    await SaveDownloadedShardAsync(relayData);
                }
            }
        }
        private static async Task RequestShardFromAllOnlineNodesAsync(string shardHash)
        {
            var onlineNodes = await Blockchain.GetOnlineNodes();
            if (!onlineNodes.Any())
            {
                Console.WriteLine($"No online peers available for {shardHash}. Trying Azure relay...");
                byte[] relayData = await NetworkService.DownloadShardFromRelay(shardHash);
                if (relayData != null)
                {
                    Console.WriteLine($"Shard {shardHash} retrieved from Azure relay (no peers online).");
                    await SaveDownloadedShardAsync(relayData);
                }
                return;
            }

            foreach (var node in onlineNodes)
            {
                await RequestShardFromNodeAsync(node, shardHash);
            }
        }

        private static async Task<bool> RequestShardFromNodeAsync(OnlineNode node, string shardHash)
        {
            if (node == null || string.IsNullOrWhiteSpace(shardHash))
                return false;

            try
            {
                await Blockchain.p2pService.PunchPeers(new List<OnlineNode> { node });

                bool isReady = await Blockchain.p2pService.CheckReadinessAsync(node);
                if (!isReady)
                {
                    Console.WriteLine($"Peer {node.dnAddress} not reachable via UDP. Trying Azure relay for {shardHash}...");
                    byte[] relayData = await NetworkService.DownloadShardFromRelay(shardHash);
                    if (relayData != null)
                    {
                        Console.WriteLine($"Shard {shardHash} retrieved from Azure relay.");
                        await SaveDownloadedShardAsync(relayData);
                        return true;
                    }
                    Console.WriteLine($"Shard {shardHash} not found on relay either.");
                    return false;
                }

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    bool sent = await Blockchain.p2pService.SendMessageAsync(node.ipAddress, node.port, "DOWNLOADSHARD", shardHash);
                    if (!sent)
                        return false;

                    await Task.Delay(250);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not request shard {shardHash} from {node.dnAddress}: {ex.Message}");
                return false;
            }
        }

        private static string CalculateShardHash(byte[] shardData)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(shardData)).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string GetRecreationInfoPath(string fileHash)
        {
            string primaryPath = Path.Combine(uploadqueuePath, $"{fileHash}.dn");
            if (File.Exists(primaryPath))
                return primaryPath;

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Uploads", $"{fileHash}.dn");
        }

        private static byte[] HexStringToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static async Task RemoveFileStateEntryAsync(string filename)
        {
            string jsonFilePath = Path.Combine(statePath, "filestate.json");
            if (!File.Exists(jsonFilePath))
                return;

            string jsonData = await File.ReadAllTextAsync(jsonFilePath);
            if (string.IsNullOrWhiteSpace(jsonData))
                return;

            var fileStateDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData);
            if (fileStateDict == null || !fileStateDict.Remove(filename))
                return;

            string updatedJson = JsonSerializer.Serialize(fileStateDict, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonFilePath, updatedJson);
        }

        private static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// DECRYPTION & RECONSTRUCTION LOGIC
        /// Reconstructs the original file from its decrypted shards.
        /// </summary>
        public static void ReconstructFile(List<byte[]> shards, string originalFilename)
        {
            Directory.CreateDirectory(downloadPath);

            string safeFileName = string.IsNullOrWhiteSpace(originalFilename)
                ? $"downloaded_{DateTime.Now:yyyyMMdd_HHmmss}.bin"
                : string.Concat(originalFilename.Split(Path.GetInvalidFileNameChars()));

            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = $"downloaded_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
            }

            string finalFilePath = Path.Combine(downloadPath, safeFileName);
            Console.WriteLine($"[LocalFileHelper] Reconstructing '{safeFileName}' from {shards.Count} shards.");

            try
            {
                string secretPath = Path.Combine(configPath, "secret.txt");
                if (!File.Exists(secretPath))
                {
                    Console.WriteLine("[LocalFileHelper] ERROR: Decryption key 'secret.txt' not found.");
                    DownloadFailed?.Invoke("Decryption key was not found for this user.");
                    return;
                }
                var (key, iv) = LoadAESKeyIV(secretPath);

                using (var fs = new FileStream(finalFilePath, FileMode.Create, FileAccess.Write))
                {
                    for (int i = 0; i < shards.Count; i++)
                    {
                        var shardData = shards[i];
                        Console.WriteLine($"[LocalFileHelper] Decrypting shard {i + 1}/{shards.Count}...");

                        byte[] decryptedShard = CryptHelper.DecryptData(shardData, key, iv);

                        if (decryptedShard != null)
                        {
                            fs.Write(decryptedShard, 0, decryptedShard.Length);
                        }
                        else
                        {
                            Console.WriteLine($"[LocalFileHelper] ERROR: Failed to decrypt shard {i + 1}. Aborting file reconstruction.");
                            fs.Close();
                            File.Delete(finalFilePath);
                            DownloadFailed?.Invoke($"Failed to decrypt shard {i + 1} for '{safeFileName}'.");
                            return;
                        }
                    }
                }
                Console.WriteLine($"[SUCCESS] File '{safeFileName}' successfully reconstructed and decrypted at {finalFilePath}!");
                DownloadCompleted?.Invoke(finalFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalFileHelper] An error occurred during file reconstruction: {ex.Message}");
                DownloadFailed?.Invoke(ex.Message);
            }
        }
    }
}
