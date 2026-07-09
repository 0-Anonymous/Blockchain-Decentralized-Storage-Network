namespace DNStoreBackend.Models
{
    public class FileManifest
    {
        public string OwnerAddress { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public ulong Size { get; set; }
        public DateTime UploadTime { get; set; }
        public List<string> ShardHashes { get; set; } = new();
    }
}
