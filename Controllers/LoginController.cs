using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UTFClassAPI;



namespace UTFClassAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly ILogger<LoginController> _logger;
        private readonly DataContext _context;
        private readonly Crypto _crypto;
        
        private const int maxAttempts = 3;
		private const int CooldownSeconds = 60;


        public LoginController(ILogger<LoginController> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
            _crypto = new Crypto();

        }

        /// <summary>
        /// Retrieves all login entries from the database. Only accessible to users with the 'Admin' role.
        /// </summary>
        [HttpGet("GetLogins")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<Login>>> Get()
        {
            try
            {
                var logins = await _context.Login.ToListAsync();
                return Ok(logins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve logins from the database.");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Authenticates a user based on username and password.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <param name="password">The password of the user.</param>
        /// <returns>JWT token for successful login, or Unauthorized for unsuccessful login.</returns>
    [HttpPost("Authenticate")]
    public async Task<ActionResult<string>> Authenticate(string username, string password)
    {
        try
        {
            // Check if the user exists and the password is correct
            var user = await _context.Login.FirstOrDefaultAsync(u => u.User == username && u.Password == password);

            if (user != null)
            {
                // User is authenticated, generate JWT token
                var token = GenerateJwtToken(user);
                return Ok(token);
            }
            else
            {
                // Delay to prevent basic attacks
                await Task.Delay(1000);

                // Check if the user has exceeded the maximum number of attempts
                if (_context.Login.Count(u => u.User == username) >= maxAttempts)
                {
                    // Block further login attempts for cooldown period
                    await Task.Delay(CooldownSeconds * 1000);
                }

                return Unauthorized("Invalid username or password.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate user.");
            return StatusCode(500, "Internal server error");
        }
    }

        /// <summary>
        /// Creates a new login entry in the database.
        /// </summary>
        /// <param name="login">The login object containing user details.</param>
        /// <returns>1 if the login is created successfully, or Unauthorized if the user is not authorized.</returns>
        [HttpPost("CreateLogin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<int>> CreateLogin(Login login)
        {
            try
            {
                _context.Login.Add(login);
                await _context.SaveChangesAsync();
                return Ok(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create login.");
                return StatusCode(500, "Internal server error");
            }
        }
        
        /// <summary>
        /// Edits an existing login entry in the database.
        /// </summary>
        /// <param name="id">The ID of the login to edit.</param>
        /// <param name="updatedLogin">The updated login object containing user details.</param>
        /// <returns>Returns the edited login object, or NotFound if the login is not found.</returns>
        [HttpPut("EditLogin/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Login), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<Login>> EditLogin(int id, Login updatedLogin)
        {
            try
            {
                var existingLogin = await _context.Login.FindAsync(id);
                if (existingLogin == null)
                {
                    return NotFound();
                }

                existingLogin.User = updatedLogin.User;
                existingLogin.Password = updatedLogin.Password; // Ensure password handling is secure
                existingLogin.IsAdmin = updatedLogin.IsAdmin;

                await _context.SaveChangesAsync();

                return Ok(existingLogin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to edit login with ID: {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes an existing login entry from the database.
        /// </summary>
        /// <param name="id">The ID of the login to delete.</param>
        /// <returns>Returns NoContent if the login is deleted successfully, or NotFound if the login is not found.</returns>
        [HttpDelete("DeleteLogin/{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> DeleteLogin(int id)
        {
            try
            {
                var existingLogin = await _context.Login.FindAsync(id);
                if (existingLogin == null)
                {
                    return NotFound();
                }

                _context.Login.Remove(existingLogin);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete login with ID: {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        private string GenerateJwtToken(Login user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_crypto.GetSecretKeyAsString());

            // Include user ID as a claim
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.User),
                new Claim(ClaimTypes.Role, user.IsAdmin == 1 ? "Admin" : "User"),
                new Claim("UserId", user.Id.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1), // Token expires in 1 hour
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
