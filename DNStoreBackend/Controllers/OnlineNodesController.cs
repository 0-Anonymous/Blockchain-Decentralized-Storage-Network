using DNStoreBackend.Models;
using Microsoft.AspNetCore.Mvc;
using DNStoreBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace DNStoreBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OnlineNodesController : ControllerBase
    {
        private readonly DNStoreDB _context;

        public OnlineNodesController(DNStoreDB context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetNodes()
        {
            var nodes = await _context.OnlineNodes
                .Where(n => n.LastSeen > DateTime.UtcNow.AddMinutes(-5))
                .ToListAsync();

            return Ok(nodes);
        }

        [HttpPost("GoOnline")]
        public async Task<IActionResult> GoOnline([FromBody] NodeDto node)
        {
            var existing = await _context.OnlineNodes
                .FirstOrDefaultAsync(n => n.DNAddress == node.DNAddress);

            if (existing != null)
            {
                existing.IPAddress = node.IPAddress;
                existing.Port = node.Port;
                existing.LastSeen = DateTime.UtcNow;
            }
            else
            {
                _context.OnlineNodes.Add(new OnlineNode
                {
                    DNAddress = node.DNAddress,
                    IPAddress = node.IPAddress,
                    Port = node.Port,
                    LastSeen = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("GoOffline")]
        public async Task<IActionResult> GoOffline([FromBody] NodeDto node)
        {
            var existing = await _context.OnlineNodes
                .FirstOrDefaultAsync(n => n.DNAddress == node.DNAddress);

            if (existing != null)
            {
                _context.OnlineNodes.Remove(existing);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}