using EquipmentTracker.Data;
using EquipmentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly DataContext _context;

        public UserService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<Users>> GetAllUsersAsync()
        {
            return await _context.Users.AsNoTracking().ToListAsync();
        }

        public async Task AddUserAsync(Users user)
        {
            // Basit validasyon
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                throw new Exception("Bu kullanıcı adı zaten kullanılıyor.");

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                // Kendini silemesin (Opsiyonel güvenlik)
                if (user.Username == "admin") throw new Exception("Ana admin silinemez.");

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}