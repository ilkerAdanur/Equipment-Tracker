using EquipmentTracker.Models;

namespace EquipmentTracker.Services.StatisticsService
{
    public interface IStatisticsService
    {
        /// <summary>
        /// Dashboard için tüm istatistikleri hesaplar ve getirir.
        /// </summary>
        Task<Statistics> GetDashboardStatisticsAsync();
    }
}
