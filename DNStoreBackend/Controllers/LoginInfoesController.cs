using DNStoreBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DNStoreBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginInfoesController : ControllerBase
    {
        private readonly DNStoreDB _context;

        public LoginInfoesController(DNStoreDB context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(User user)
        {
            if (_context.Users.Any(u => u.EmailId == user.EmailId))
                return BadRequest("Email already exists");

            if (_context.Users.Any(u => u.Username == user.Username))
                return BadRequest("User already exists");

            user.DNAddress = Guid.NewGuid().ToString();

            _context.Users.Add(user);                 // ✅ ONLY DB
            await _context.SaveChangesAsync();        // ✅ SAVE

            return Ok("Registered successfully");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest loginUser)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Username == loginUser.Username &&
                    u.Password == loginUser.Password);

            if (user == null)
                return Unauthorized();

            return Ok(user.DNAddress);
        }
    }
}