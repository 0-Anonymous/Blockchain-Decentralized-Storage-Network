using DNStoreBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNStoreBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShardsController : ControllerBase
    {
        private readonly string storagePath = Path.Combine(Directory.GetCurrentDirectory(), "relay_storage");
        private readonly DNStoreDB _context;

        public ShardsController(DNStoreDB context)
        {
            _context = context;
            if (!Directory.Exists(storagePath))
                Directory.CreateDirectory(storagePath);
        }

        // POST api/Shards/{shardHash}
        // Called by the uploader when UDP hole punching fails.
        // Accepts raw bytes in the request body.
        [HttpPost("{shardHash}")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadRelayShard(string shardHash)
        {
            // Clean up expired shards first
            await CleanupExpiredAsync();

            // Check if this shard is already relayed (idempotent)
            var existing = await _context.ShardRelays
                .FirstOrDefaultAsync(s => s.ShardHash == shardHash);
            if (existing != null)
                return Ok(); // Already stored

            // Read raw bytes from request body
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            byte[] data = ms.ToArray();

            if (data.Length == 0)
                return BadRequest("Empty shard body.");

            // Save to disk
            string filePath = Path.Combine(storagePath, shardHash + ".relay");
            await System.IO.File.WriteAllBytesAsync(filePath, data);

            // Record in DB with 30-minute expiry
            _context.ShardRelays.Add(new ShardRelay
            {
                ShardHash = shardHash,
                FilePath = filePath,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET api/Shards/{shardHash}
        // Called by the downloader when UDP hole punching fails.
        // Returns the shard bytes once, then deletes the relay entry.
        [HttpGet("{shardHash}")]
        public async Task<IActionResult> DownloadRelayShard(string shardHash)
        {
            var entry = await _context.ShardRelays
                .FirstOrDefaultAsync(s => s.ShardHash == shardHash
                                       && s.ExpiresAt > DateTime.UtcNow);

            if (entry == null || !System.IO.File.Exists(entry.FilePath))
                return NotFound();

            byte[] data = await System.IO.File.ReadAllBytesAsync(entry.FilePath);

            // One-time pickup: delete from DB and disk
            System.IO.File.Delete(entry.FilePath);
            _context.ShardRelays.Remove(entry);
            await _context.SaveChangesAsync();

            return File(data, "application/octet-stream");
        }

        private async Task CleanupExpiredAsync()
        {
            var expired = await _context.ShardRelays
                .Where(s => s.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            foreach (var e in expired)
            {
                if (System.IO.File.Exists(e.FilePath))
                    System.IO.File.Delete(e.FilePath);
            }

            _context.ShardRelays.RemoveRange(expired);
            await _context.SaveChangesAsync();
        }
    }
}