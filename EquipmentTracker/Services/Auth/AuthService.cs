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

        public async Task<Users> LoginAsync(string username, string password)
        {
            try
            {
                // 1. Önce eski oturumları temizle
                await CleanupOfflineUsersAsync();

                // 2. Kullanıcıyı bul (Okuma amaçlı)
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                if (user != null)
                {
                    // 3. Güncellemek için izlenen (tracked) nesneyi çek
                    var userToUpdate = await _context.Users.FindAsync(user.Id);

                    if (userToUpdate != null)
                    {
                        userToUpdate.IsOnline = true;
                        userToUpdate.LastActive = DateTime.Now; // <-- BU SATIR EKSİKTİ, EKLENDİ

                        await _context.SaveChangesAsync();

                        // Takibi bırak (Döndürülen nesne detached olsun)
                        _context.Entry(userToUpdate).State = EntityState.Detached;

                        // Döndürülecek nesneyi de güncelle (UI için)
                        user.IsOnline = true;
                        user.LastActive = DateTime.Now;
                    }
                }

                return user;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task UpdateLastActiveAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastActive = DateTime.Now;
                user.IsOnline = true;
                await _context.SaveChangesAsync();

                // Takibi bırak
                _context.Entry(user).State = EntityState.Detached;
            }
        }
        private async Task CleanupOfflineUsersAsync()
        {
            try
            {
                // Son 5 dakikadır aktif olmayan ama hala Online görünenleri Offline yap
                // SQL Raw komutu en performanslısıdır
                var timeThreshold = DateTime.Now.AddMinutes(-5);

                // Entity Framework Core ile Raw SQL çalıştırıyoruz
                // MySQL formatı: 'YYYY-MM-DD HH:mm:ss'
                string formattedTime = timeThreshold.ToString("yyyy-MM-dd HH:mm:ss");

                string sql = $"UPDATE Users SET IsOnline = 0 WHERE IsOnline = 1 AND (LastActive < '{formattedTime}' OR LastActive IS NULL)";

                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Temizlik Hatası: {ex.Message}");
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
        public async Task<List<Users>> GetActiveUsersAsync()
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