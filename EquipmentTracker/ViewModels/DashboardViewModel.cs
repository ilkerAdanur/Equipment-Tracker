using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EquipmentTracker.Models;
using EquipmentTracker.Services.Auth;
using EquipmentTracker.Services.Job;
using EquipmentTracker.Services.StatisticsService;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace EquipmentTracker.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IJobService _jobService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        Statistics _stats;

        // YENİ: Bağlantı Hatası Durumu
        [ObservableProperty]
        bool _isConnectionError;

        // YENİ: Hata Mesajı
        [ObservableProperty]
        string _connectionErrorMessage;

        public ObservableCollection<User> ActiveUsers { get; set; } = new();
        public bool IsAdminUser => App.CurrentUser?.IsAdmin ?? false;

        public DashboardViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Title = "Dashboard";
            Stats = new Statistics(); // Başlangıçta boş

            // --- MESAJI DİNLE ---
            WeakReferenceMessenger.Default.Register<ConnectionMessage>(this, (r, m) =>
            {
                if (m.Value) // Bağlandıysa
                {
                    // Verileri Yükle
                    LoadStatisticsCommand.Execute(null);
                }
                else // Bağlantı Kesildiyse
                {
                    // Verileri Temizle
                    Stats = new Statistics();
                    IsConnectionError = false; // Hata mesajını da kaldır
                    ConnectionErrorMessage = string.Empty;
                    ActiveUsers.Clear();
                }
            });
        }

        // YENİ: Ayarlar sayfasına gitme komutu
        [RelayCommand]
        async Task GoToSettings()
        {
            // Shell rotasıyla Settings tab'ına git
            await Shell.Current.GoToAsync("//SettingsPage");
        }

        [RelayCommand]
        async Task LoadStatisticsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                    var statsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

                    // YENİ: Auth servisi de çağır
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                    if (!MauiProgram.IsDatabaseInitialized)
                    {
                        await jobService.InitializeDatabaseAsync();
                        MauiProgram.IsDatabaseInitialized = true;
                    }

                    Stats = await statsService.GetDashboardStatisticsAsync();

                    // YENİ: Eğer Adminseniz, aktif kullanıcıları da yükle
                    if (IsAdminUser)
                    {
                        var users = await authService.GetActiveUsersAsync();
                        ActiveUsers.Clear();
                        foreach (var u in users)
                        {
                            // Kendimizi listede göstermeyebiliriz veya gösterebiliriz
                            ActiveUsers.Add(u);
                        }
                        OnPropertyChanged(nameof(IsAdminUser)); // UI'ı tetikle
                    }
                }
            }
            catch (Exception ex)
            {
                // ... Hata yönetimi ...
                Stats = new Statistics();
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task DisconnectUser(User userToDisconnect)
        {
            if (userToDisconnect == null) return;

            // Kendini atmasın
            if (userToDisconnect.Id == App.CurrentUser?.Id)
            {
                await Shell.Current.DisplayAlert("Uyarı", "Kendi bağlantınızı buradan kesemezsiniz, çıkış yapın.", "Tamam");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert("Onay",
                $"{userToDisconnect.Username} kullanıcısının bağlantısını kesmek istiyor musunuz?", "Evet", "Hayır");

            if (!confirm) return;

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                    await authService.DisconnectUserAsync(userToDisconnect.Id);
                }

                // Listeden hemen sil (UI güncellensin)
                ActiveUsers.Remove(userToDisconnect);

                await Shell.Current.DisplayAlert("Başarılı", "Kullanıcı bağlantısı kesildi (Offline yapıldı).", "Tamam");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
        }
    }
}