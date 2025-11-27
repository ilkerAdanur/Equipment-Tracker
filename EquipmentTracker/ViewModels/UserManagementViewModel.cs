using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentTracker.Models; // <-- BU SATIR EKSİKTİ, HATAYI ÇÖZER
using EquipmentTracker.Services.UserService;
using System.Collections.ObjectModel;

namespace EquipmentTracker.ViewModels
{
    public partial class UserManagementViewModel : BaseViewModel
    {
        private readonly IUserService _userService;

        public ObservableCollection<Users> Users { get; } = new();

        [ObservableProperty]
        string _newUsername;

        [ObservableProperty]
        string _newPassword;

        [ObservableProperty]
        string _newFullName;

        [ObservableProperty]
        bool _newIsAdmin;



        public UserManagementViewModel(IUserService userService)
        {
            _userService = userService;
            Title = "Kullanıcı Yönetimi";
            _ = LoadUsers();
        }

        [RelayCommand]
        async Task LoadUsers()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                Users.Clear();
                var list = await _userService.GetAllUsersAsync();
                foreach (var user in list)
                {
                    Users.Add(user);
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task AddUser()
        {
            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
            {
                await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı adı ve şifre boş olamaz.", "Tamam");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var newUser = new Users
                {
                    Username = NewUsername,
                    Password = NewPassword,
                    FullName = NewFullName,
                    IsAdmin = NewIsAdmin,
                    IsOnline = false
                };

                await _userService.AddUserAsync(newUser);

                NewUsername = "";
                NewPassword = "";
                NewFullName = "";
                NewIsAdmin = false;

                await LoadUsers();
                await Application.Current.MainPage.DisplayAlert("Başarılı", "Kullanıcı eklendi.", "Tamam");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        async Task DeleteUser(Users user)
        {
            if (user == null) return;
            bool answer = await Application.Current.MainPage.DisplayAlert("Sil", $"{user.Username} kullanıcısını silmek istiyor musunuz?", "Evet", "Hayır");
            if (!answer) return;

            try
            {
                await _userService.DeleteUserAsync(user.Id);
                Users.Remove(user);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Hata", ex.Message, "Tamam");
            }
        }
    }
}