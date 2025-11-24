using EquipmentTracker.Models;

namespace EquipmentTracker.Services.Auth
{
    public interface IAuthService
    {
        Task<Users> LoginAsync(string username, string password);
        Task LogoutAsync(int userId); 
        Task<List<Users>> GetActiveUsersAsync(); 
        Task DisconnectUserAsync(int userId);
        Task<bool> IsUserActiveAsync(int userId);
    }
}