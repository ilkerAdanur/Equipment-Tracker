using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly DataContext _context;

        public AuthService(DataContext context)
        {
            _context = context;
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                // Kullanıcıyı online olarak işaretle
                user.IsOnline = true;
                await _context.SaveChangesAsync();
            }

            return user;
        }

        public async Task LogoutAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                await _context.SaveChangesAsync();
                _context.Entry(user).State = EntityState.Detached;
            }
        }

        // Sadece Online olanları getir
        public async Task<List<User>> GetActiveUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsOnline == true)
                .ToListAsync();
        }

        // Admin birini attığında çalışacak
        public async Task DisconnectUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                await _context.SaveChangesAsync();
                _context.Entry(user).State = EntityState.Detached;
            }
        }
    }
}