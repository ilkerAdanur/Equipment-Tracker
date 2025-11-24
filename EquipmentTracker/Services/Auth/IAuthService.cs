using EquipmentTracker.Models;

namespace EquipmentTracker.Services.Auth
{
    public interface IAuthService
    {
        Task<User> LoginAsync(string username, string password);
        Task LogoutAsync(int userId); 
        Task<List<User>> GetActiveUsersAsync(); 
        Task DisconnectUserAsync(int userId);
        Task<bool> IsUserActiveAsync(int userId);
    }
}