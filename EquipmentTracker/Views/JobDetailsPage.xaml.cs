using EquipmentTracker.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Reflection;
#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
#endif

namespace EquipmentTracker.Views;

public partial class JobDetailsPage : ContentPage
{
    // Scroll pozisyonunu tutacak değişken
    private double _lastScrollY = 0;

    public JobDetailsPage(JobDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Sayfadan başka uygulamaya geçince veya geri çıkınca çalışır
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Mevcut kaydırma pozisyonunu kaydet
        _lastScrollY = MainScroll.ScrollY;
    }

    // Sayfaya geri dönünce çalışır
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ViewModel veriyi yüklüyor olabilir, biraz bekleyelim
        // Eğer ViewModel'inizde OnAppearing load işlemi varsa, o listenin oluşması zaman alır.
        if (BindingContext is JobDetailsViewModel vm)
        {
            // Eğer veri yoksa yükle, varsa elleme (Burası opsiyonel iyileştirme)
            // await vm.LoadDataCommand.ExecuteAsync(null); 
        }

        // UI'ın kendine gelmesi için çok kısa bekle
        await Task.Delay(100);

        // Kaydedilen pozisyona geri git (Animasyonsuz: false)
        if (_lastScrollY > 0)
        {
            await MainScroll.ScrollToAsync(0, _lastScrollY, false);
        }
    }

    async void ThumbnailPointerEntered(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
    {
        if (sender is not Image image || image.Source == null)
            return;

        AttachmentPreviewImage.Source = image.Source;
        PositionPreview(image);
        AttachmentPreview.Opacity = 0;
        AttachmentPreview.IsVisible = true;
        await AttachmentPreview.FadeTo(1, 100);

#if WINDOWS
        SetCursor(image, true);
#endif
    }

    async void ThumbnailPointerExited(object? sender, Microsoft.Maui.Controls.PointerEventArgs e)
    {
        if (!AttachmentPreview.IsVisible)
            return;

#if WINDOWS
        if (sender is Image image)
            SetCursor(image, false);
#endif

        await AttachmentPreview.FadeTo(0, 100);
        AttachmentPreview.IsVisible = false;
    }

    void PositionPreview(Image sourceImage)
    {
        var relativePoint = GetRelativePosition(sourceImage, RootGrid);
        double overlayWidth = AttachmentPreview.Width > 0 ? AttachmentPreview.Width : AttachmentPreview.WidthRequest;
        double overlayHeight = AttachmentPreview.Height > 0 ? AttachmentPreview.Height : AttachmentPreview.HeightRequest;

        double targetX = relativePoint.X + sourceImage.Width + 12;
        if (targetX + overlayWidth > RootGrid.Width)
        {
            targetX = Math.Max(0, relativePoint.X - overlayWidth - 12);
        }

        double targetY = relativePoint.Y - (overlayHeight - sourceImage.Height) / 2;
        targetY = Math.Clamp(targetY, 0, Math.Max(0, RootGrid.Height - overlayHeight));

        AttachmentPreview.TranslationX = targetX;
        AttachmentPreview.TranslationY = targetY;
    }

    Point GetRelativePosition(VisualElement element, VisualElement relativeTo)
    {
        double x = element.X;
        double y = element.Y;
        var parent = element.Parent as VisualElement;

        while (parent != null && parent != relativeTo)
        {
            x += parent.X;
            y += parent.Y;

            if (parent is ScrollView scroll)
            {
                x -= scroll.ScrollX;
                y -= scroll.ScrollY;
            }

            parent = parent.Parent as VisualElement;
        }

        return new Point(x, y);
    }

#if WINDOWS
    static void SetCursor(Image image, bool isHand)
    {
        if (image.Handler?.PlatformView is UIElement element)
        {
            var propertyInfo = typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null)
                return;

            var cursor = isHand
                ? InputSystemCursor.Create(InputSystemCursorShape.Hand)
                : null;

            propertyInfo.SetValue(element, cursor);
        }
    }
#endif
}