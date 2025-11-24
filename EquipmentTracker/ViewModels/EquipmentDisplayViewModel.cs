using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentTracker.Models;
using System.Collections.ObjectModel;

namespace EquipmentTracker.ViewModels;

public partial class EquipmentDisplayViewModel : ObservableObject
{
    public Equipment EquipmentModel { get; set; }

    [ObservableProperty]
    string fullEquipmentNo; // "003.001" gibi

    [ObservableProperty]
    string equipmentName;

    public ObservableCollection<EquipmentPartDisplayViewModel> EquipmentParts { get; set; } = new();
}