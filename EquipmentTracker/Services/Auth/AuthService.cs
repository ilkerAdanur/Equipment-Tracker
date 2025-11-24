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
            try
            {
                // AsNoTracking performans artırır
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                if (user != null)
                {
                    // Kullanıcıyı online yap
                    var userToUpdate = await _context.Users.FindAsync(user.Id);
                    if (userToUpdate != null)
                    {
                        userToUpdate.IsOnline = true;
                        await _context.SaveChangesAsync();
                    }
                }

                return user;
            }
            catch (Exception)
            {
                // Hatayı burada yutma (catch boş olmasın), yukarı fırlat!
                // Böylece LoginViewModel hatayı yakalar ve ekrana basar.
                throw;
            }
        }

        public async Task LogoutAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = false;
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // Çıkış yaparken hata olursa çok önemli değil, kullanıcıyı üzmeyelim.
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

        public async Task<bool> IsUserActiveAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            return user != null && user.IsOnline;
        }
    }
}