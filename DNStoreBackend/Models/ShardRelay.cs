namespace DNStoreBackend.Models
{
    public class ShardRelay
    {
        public int Id { get; set; }
        public string ShardHash { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}