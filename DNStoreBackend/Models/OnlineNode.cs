public class OnlineNode
{
    public int Id { get; set; }
    public string DNAddress { get; set; }
    public string IPAddress { get; set; }
    public int Port { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}