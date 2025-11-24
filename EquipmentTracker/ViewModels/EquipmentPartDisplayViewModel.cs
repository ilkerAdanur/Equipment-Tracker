using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentTracker.Models;

namespace EquipmentTracker.ViewModels;

public partial class EquipmentPartDisplayViewModel : ObservableObject
{
    public EquipmentPart PartModel { get; set; }

    [ObservableProperty]
    string fullPartNo; // "003.001.001" gibi

    [ObservableProperty]
    string partName;
}