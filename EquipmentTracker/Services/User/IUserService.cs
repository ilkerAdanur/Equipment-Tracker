using EquipmentTracker.Models;

namespace EquipmentTracker.Services.UserService
{
    public interface IUserService
    {
        Task<List<Users>> GetAllUsersAsync();
        Task AddUserAsync(Users user);
        Task DeleteUserAsync(int userId);
    }
}