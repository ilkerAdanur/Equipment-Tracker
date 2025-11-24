using CommunityToolkit.Mvvm.ComponentModel;

namespace EquipmentTracker.Models
{
    // 'partial' ve 'ObservableObject' olması şart
    public partial class User : ObservableObject
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public bool IsAdmin { get; set; }

        [ObservableProperty]
        private bool _isOnline;
    }
}