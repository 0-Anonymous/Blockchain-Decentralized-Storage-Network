using Microsoft.EntityFrameworkCore;

namespace DNStoreBackend.Models
{
    public class DNStoreDB : DbContext
    {
        public DNStoreDB(DbContextOptions<DNStoreDB> options)
            : base(options)
        {
        }

        public DbSet<OnlineNode> OnlineNodes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ShardRelay> ShardRelays { get; set; }
    }
}