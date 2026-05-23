using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PicOrient.Models;
using PicOrient.Services;
using System.Collections.Generic;

namespace PicOrient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private ScanResult? _scanResult;
    private CancellationTokenSource? _cts;

    public MainViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        LoadFromSettings();
    }

    // ── フォルダ選択 ──────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceFolder))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private string _sourceFolder = "";

    public bool HasSourceFolder => !string.IsNullOrEmpty(SourceFolder);

    // ── 出力先 ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseCustomOutputFolder))]
    [NotifyPropertyChangedFor(nameof(CustomOutputRowVisibility))]
    private bool _useSourceFolderAsOutput = true;

    public bool UseCustomOutputFolder
    {
        get => !UseSourceFolderAsOutput;
        set => UseSourceFolderAsOutput = !value;
    }

    public Visibility CustomOutputRowVisibility =>
        UseCustomOutputFolder ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private string _customOutputFolder = "";

    // ── 操作設定 ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMove))]
    private bool _isCopy = true;

    public bool IsMove
    {
        get => !IsCopy;
        set => IsCopy = !value;
    }

    // 重複処理
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateSkip))]
    [NotifyPropertyChangedFor(nameof(DuplicateOverwrite))]
    [NotifyPropertyChangedFor(nameof(DuplicateRename))]
    private DuplicateHandling _duplicateHandling = DuplicateHandling.Skip;

    public bool DuplicateSkip
    {
        get => DuplicateHandling == DuplicateHandling.Skip;
        set { if (value) DuplicateHandling = DuplicateHandling.Skip; }
    }
    public bool DuplicateOverwrite
    {
        get => DuplicateHandling == DuplicateHandling.Overwrite;
        set { if (value) DuplicateHandling = DuplicateHandling.Overwrite; }
    }
    public bool DuplicateRename
    {
        get => DuplicateHandling == DuplicateHandling.AutoRename;
        set { if (value) DuplicateHandling = DuplicateHandling.AutoRename; }
    }

    [ObservableProperty] private double _aspectRatioThreshold = 1.1;
    [ObservableProperty] private bool _processSubfolders = false;
    [ObservableProperty] private string _portraitFolderName = "縦";
    [ObservableProperty] private string _landscapeFolderName = "横";
    [ObservableProperty] private string _squareFolderName = "中間";

    // ── スキャン結果 ──────────────────────────────────────────
    [ObservableProperty] private int _portraitCount;
    [ObservableProperty] private int _landscapeCount;
    [ObservableProperty] private int _squareCount;
    [ObservableProperty] private int _totalCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SortCommand))]
    private bool _hasScanResult;

    // ── エラー通知 ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSortErrors))]
    [NotifyPropertyChangedFor(nameof(SortErrorsTitle))]
    [NotifyPropertyChangedFor(nameof(SortErrorsText))]
    private IReadOnlyList<SortError>? _sortErrors;

    public bool ShowSortErrors => SortErrors != null && SortErrors.Count > 0;

    public string SortErrorsTitle =>
        SortErrors == null ? "" : $"エラーが {SortErrors.Count:N0} 件発生しました";

    public string SortErrorsText
    {
        get
        {
            if (SortErrors == null || SortErrors.Count == 0) return "";
            const int MaxShow = 20;
            var lines = SortErrors.Take(MaxShow)
                .Select(e => $"・{e.FileName} — {e.Reason}");
            var text = string.Join("\n", lines);
            if (SortErrors.Count > MaxShow)
                text += $"\n（他 {SortErrors.Count - MaxShow} 件）";
            return text;
        }
    }

    [RelayCommand]
    private void DismissSortErrors() => SortErrors = null;

    // ── 処理状態 ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(SortCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(ScanCancelVisibility))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(SortCancelVisibility))]
    private bool _isSorting;

    public Visibility ScanButtonVisibility  => IsScanning  ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ScanCancelVisibility  => IsScanning  ? Visibility.Visible   : Visibility.Collapsed;
    public Visibility SortButtonVisibility  => IsSorting   ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SortCancelVisibility  => IsSorting   ? Visibility.Visible   : Visibility.Collapsed;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgressVisibility))]
    private bool _showProgress;

    public Visibility ShowProgressVisibility =>
        ShowProgress ? Visibility.Visible : Visibility.Collapsed;

    // ── コマンド ──────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectSourceFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) SourceFolder = folder.Path;
    }

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) CustomOutputFolder = folder.Path;
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        _cts = new CancellationTokenSource();
        IsProcessing = true;
        IsScanning = true;
        ShowProgress = true;
        ProgressValue = 0;
        HasScanResult = false;
        StatusMessage = "スキャン中...";

        bool completed = false;
        try
        {
            var targetFolders = new[] { PortraitFolderName, LandscapeFolderName, SquareFolderName };
            var progress = new Progress<ScanProgress>(p =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (!IsProcessing) return;
                    ProgressValue = (double)p.Processed / p.Total * 100;
                    StatusMessage = $"スキャン中: {p.CurrentFile}  ({p.Processed:N0} / {p.Total:N0})";
                });
            });

            _scanResult = await ImageScannerService.ScanAsync(
                SourceFolder, ProcessSubfolders, AspectRatioThreshold,
                targetFolders, progress, _cts.Token);

            PortraitCount = _scanResult.PortraitImages.Count;
            LandscapeCount = _scanResult.LandscapeImages.Count;
            SquareCount = _scanResult.SquareImages.Count;
            TotalCount = _scanResult.TotalCount;
            HasScanResult = TotalCount > 0;

            var skippedMsg = _scanResult.SkippedCount > 0 ? $"（スキップ {_scanResult.SkippedCount:N0} 枚）" : "";
            StatusMessage = $"スキャン完了。縦 {PortraitCount:N0} 枚・横 {LandscapeCount:N0} 枚・中間 {SquareCount:N0} 枚 {skippedMsg}";
            completed = true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "キャンセルしました";
            _scanResult = null;
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "スキャン");
            StatusMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            IsScanning = false;
            if (!completed) ProgressValue = 0;
        }
    }

    private bool CanScan() => HasSourceFolder && !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanSort))]
    private async Task SortAsync()
    {
        if (_scanResult == null) return;

        var outputRoot = UseSourceFolderAsOutput ? SourceFolder : CustomOutputFolder;
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            StatusMessage = "出力先フォルダを指定してください";
            return;
        }

        _cts = new CancellationTokenSource();
        IsProcessing = true;
        IsSorting = true;
        ShowProgress = true;
        ProgressValue = 0;
        SortErrors = null;
        StatusMessage = "振り分け中...";

        bool completed = false;
        try
        {
            var settings = BuildCurrentSettings();
            var progress = new Progress<SortProgress>(p =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (!IsProcessing) return;
                    ProgressValue = (double)p.Processed / p.Total * 100;
                    StatusMessage = $"処理中: {p.CurrentFile}  ({p.Processed:N0} / {p.Total:N0})";
                });
            });

            var sortErrors = await ImageSorterService.SortAsync(_scanResult, outputRoot, settings, progress, _cts.Token);
            ProgressValue = 100;
            if (sortErrors.Count > 0) SortErrors = sortErrors;
            var errorMsg = sortErrors.Count > 0 ? $"（エラー {sortErrors.Count:N0} 件）" : "";
            StatusMessage = $"完了。縦 {PortraitCount:N0} 枚・横 {LandscapeCount:N0} 枚・中間 {SquareCount:N0} 枚 {errorMsg}";
            App.Log.Info($"振り分け完了: {outputRoot}");
            completed = true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "キャンセルしました";
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "振り分け");
            StatusMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            IsSorting = false;
            if (!completed) ProgressValue = 0;
        }
    }

    private bool CanSort() => HasScanResult && !IsProcessing;

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    // ── ドロップ処理 ──────────────────────────────────────────
    public void SetSourceFolderFromDrop(string folderPath)
    {
        SourceFolder = folderPath;
    }

    // ── 設定の読み書き ────────────────────────────────────────
    private void LoadFromSettings()
    {
        var s = App.Settings.Current;
        PortraitFolderName = s.PortraitFolderName;
        LandscapeFolderName = s.LandscapeFolderName;
        SquareFolderName = s.SquareFolderName;
        IsCopy = s.Operation == OperationMode.Copy;
        DuplicateHandling = s.DuplicateHandling;
        AspectRatioThreshold = s.AspectRatioThreshold;
        ProcessSubfolders = s.ProcessSubfolders;
        UseSourceFolderAsOutput = s.UseSourceFolderAsOutput;
        CustomOutputFolder = s.CustomOutputFolder;
        SourceFolder = s.LastSourceFolder;
    }

    public void SaveToSettings()
    {
        var s = App.Settings.Current;
        s.PortraitFolderName = PortraitFolderName;
        s.LandscapeFolderName = LandscapeFolderName;
        s.SquareFolderName = SquareFolderName;
        s.Operation = IsCopy ? OperationMode.Copy : OperationMode.Move;
        s.DuplicateHandling = DuplicateHandling;
        s.AspectRatioThreshold = AspectRatioThreshold;
        s.ProcessSubfolders = ProcessSubfolders;
        s.UseSourceFolderAsOutput = UseSourceFolderAsOutput;
        s.CustomOutputFolder = CustomOutputFolder;
        s.LastSourceFolder = SourceFolder;
        App.Settings.Save();
    }

    private AppSettings BuildCurrentSettings() => new()
    {
        PortraitFolderName = PortraitFolderName,
        LandscapeFolderName = LandscapeFolderName,
        SquareFolderName = SquareFolderName,
        Operation = IsCopy ? OperationMode.Copy : OperationMode.Move,
        DuplicateHandling = DuplicateHandling,
    };
}
