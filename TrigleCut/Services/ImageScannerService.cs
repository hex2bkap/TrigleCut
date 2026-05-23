using Windows.Storage;
using Windows.Storage.FileProperties;
using TrigleCut.Models;

namespace TrigleCut.Services;

public enum ImageOrientation { Portrait, Landscape, Square }

public record ImageFileInfo(string Path, uint DisplayWidth, uint DisplayHeight)
{
    public ImageOrientation GetOrientation(double threshold)
    {
        if (DisplayWidth == 0 || DisplayHeight == 0) return ImageOrientation.Square;
        double ratio = (double)Math.Max(DisplayWidth, DisplayHeight) / Math.Min(DisplayWidth, DisplayHeight);
        if (ratio < threshold) return ImageOrientation.Square;
        return DisplayWidth < DisplayHeight ? ImageOrientation.Portrait : ImageOrientation.Landscape;
    }
}

public class ScanResult
{
    public List<ImageFileInfo> PortraitImages { get; } = [];
    public List<ImageFileInfo> LandscapeImages { get; } = [];
    public List<ImageFileInfo> SquareImages { get; } = [];
    public int SkippedCount { get; set; }
    public int TotalCount => PortraitImages.Count + LandscapeImages.Count + SquareImages.Count;
}

public record ScanProgress(int Processed, int Total, string CurrentFile);

public static class ImageScannerService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tif", ".heic", ".heif" };

    public static async Task<ScanResult> ScanAsync(
        string folderPath,
        bool recursive,
        double threshold,
        string[] targetFolderNames,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ScanResult();

        var fileList = CollectFiles(folderPath, recursive, folderPath, targetFolderNames);
        int total = fileList.Count;
        int processed = 0;
        int skippedCount = 0;

        // 並列4で処理（I/Oバウンドのため適度な並列数）
        var semaphore = new SemaphoreSlim(4);
        var portraits = new System.Collections.Concurrent.ConcurrentBag<ImageFileInfo>();
        var landscapes = new System.Collections.Concurrent.ConcurrentBag<ImageFileInfo>();
        var squares = new System.Collections.Concurrent.ConcurrentBag<ImageFileInfo>();
        var tasks = new List<Task>();

        foreach (var filePath in fileList)
        {
            ct.ThrowIfCancellationRequested();
            var path = filePath;

            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var info = await GetImageInfoAsync(path);
                    if (info != null)
                    {
                        switch (info.GetOrientation(threshold))
                        {
                            case ImageOrientation.Portrait: portraits.Add(info); break;
                            case ImageOrientation.Landscape: landscapes.Add(info); break;
                            case ImageOrientation.Square: squares.Add(info); break;
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    App.Log.Error(ex, path);
                    Interlocked.Increment(ref skippedCount);
                }
                finally
                {
                    semaphore.Release();
                    int done = Interlocked.Increment(ref processed);
                    if (done % 100 == 0 || done == total)
                        progress?.Report(new ScanProgress(done, total, System.IO.Path.GetFileName(path)));
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        result.SkippedCount = skippedCount;

        result.PortraitImages.AddRange(portraits);
        result.LandscapeImages.AddRange(landscapes);
        result.SquareImages.AddRange(squares);

        return result;
    }

    private static List<string> CollectFiles(
        string rootFolder,
        bool recursive,
        string outputBase,
        string[] targetFolderNames)
    {
        var files = new List<string>();
        CollectFilesRecursive(rootFolder, files, recursive, outputBase, targetFolderNames);
        return files;
    }

    private static void CollectFilesRecursive(
        string dir,
        List<string> files,
        bool recursive,
        string outputBase,
        string[] targetFolderNames)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                if (SupportedExtensions.Contains(System.IO.Path.GetExtension(f)))
                    files.Add(f);
            }

            if (!recursive) return;

            foreach (var subDir in Directory.EnumerateDirectories(dir))
            {
                var dirName = System.IO.Path.GetFileName(subDir);
                // 振り分け先フォルダ自体は処理しない
                if (targetFolderNames.Contains(dirName, StringComparer.OrdinalIgnoreCase)) continue;
                CollectFilesRecursive(subDir, files, true, outputBase, targetFolderNames);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            App.Log.Warn($"アクセス拒否: {dir} - {ex.Message}");
        }
    }

    private static async Task<ImageFileInfo?> GetImageInfoAsync(string filePath)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var props = await file.Properties.GetImagePropertiesAsync();

            uint width = props.Width;
            uint height = props.Height;

            if (width == 0 || height == 0) return null;

            // GetImagePropertiesAsync() はEXIF回転を適用済みの表示サイズを返すため
            // 追加の回転補正は不要

            return new ImageFileInfo(filePath, width, height);
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, filePath);
            return null;
        }
    }
}
