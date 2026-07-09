using DNStoreBackend.Models;

namespace DNStoreBackend
{
    public static class DataStore
    {
        public static List<User> Users = new();
        public static List<OnlineNode> OnlineNodes = new();
    }
}