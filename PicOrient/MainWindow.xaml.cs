using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PicOrient.Helpers;
using PicOrient.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PicOrient;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private static readonly string AppVersion = "1.0.0";
    private static readonly string AppName = "PicOrient";

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(DispatcherQueue);
        WindowHelper.RestorePosition(this, App.Settings.Current);
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        WindowHelper.SavePosition(this, App.Settings.Current);
        ViewModel.SaveToSettings();
    }

    // ── ドラッグ&ドロップ ─────────────────────────────────────
    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride != null)
            e.DragUIOverride.Caption = "フォルダを選択";
    }

    private void DropArea_DragEnter(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    private void DropArea_DragLeave(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
    }

    private async void DropArea_Drop(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            var folder = items.OfType<StorageFolder>().FirstOrDefault();
            if (folder != null)
                ViewModel.SetSourceFolderFromDrop(folder.Path);
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "ドロップ");
            ViewModel.StatusMessage = $"フォルダの取得に失敗しました: {ex.Message}";
        }
    }

    // ── InfoBar ───────────────────────────────────────────────
    private void SortErrorInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissSortErrorsCommand.Execute(null);
    }

    // ── ヘルプメニュー ────────────────────────────────────────
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PicOrient", "logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo("explorer.exe", logDir) { UseShellExecute = true });
    }

    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "バージョン情報",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = AppName, Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] },
                    new TextBlock { Text = $"バージョン {AppVersion}" },
                    new TextBlock
                    {
                        Text = "画像ファイルを縦・横・中間に自動で振り分けるツールです。",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            CloseButtonText = "閉じる",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
