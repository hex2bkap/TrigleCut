using TrigleCut.Models;

namespace TrigleCut.Services;

public record SortProgress(int Processed, int Total, string CurrentFile);
public record SortError(string FileName, string Reason);

public class SortException(string filePath, Exception inner)
    : Exception($"ファイル処理エラー: {filePath}", inner)
{
    public string FilePath { get; } = filePath;
}

public static class ImageSorterService
{
    public static async Task<List<SortError>> SortAsync(
        ScanResult scanResult,
        string outputRoot,
        AppSettings settings,
        IProgress<SortProgress>? progress = null,
        CancellationToken ct = default)
    {
        var allFiles = new List<(ImageFileInfo Info, string TargetFolder)>();
        foreach (var img in scanResult.PortraitImages)
            allFiles.Add((img, settings.PortraitFolderName));
        foreach (var img in scanResult.LandscapeImages)
            allFiles.Add((img, settings.LandscapeFolderName));
        foreach (var img in scanResult.SquareImages)
            allFiles.Add((img, settings.SquareFolderName));

        // 出力フォルダをあらかじめ作成
        EnsureOutputFolders(outputRoot, settings);

        int total = allFiles.Count;
        int processed = 0;
        var errors = new List<SortError>();

        foreach (var (img, folderName) in allFiles)
        {
            ct.ThrowIfCancellationRequested();

            var destDir = Path.Combine(outputRoot, folderName);
            var destPath = BuildDestPath(destDir, img.Path, settings.DuplicateHandling);

            if (destPath == null)
            {
                App.Log.Info($"スキップ: {img.Path}");
            }
            else
            {
                try
                {
                    await Task.Run(() => ExecuteFileOperation(img.Path, destPath, settings.Operation), ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(new SortError(Path.GetFileName(img.Path), SimplifyException(ex)));
                }
            }

            processed++;
            if (processed % 50 == 0 || processed == total)
                progress?.Report(new SortProgress(processed, total, Path.GetFileName(img.Path)));
        }

        return errors;
    }

    private static string SimplifyException(Exception ex)
    {
        var inner = ex is SortException se ? se.InnerException ?? ex : ex;
        return inner switch
        {
            IOException ioe when ioe.HResult == unchecked((int)0x80070017) => "CRCエラー（ファイル破損）",
            IOException ioe when ioe.HResult == unchecked((int)0x80070570) => "ファイルまたはディレクトリが壊れています",
            UnauthorizedAccessException => "アクセス権限がありません",
            IOException => "ファイルI/Oエラー",
            _ => "処理エラー"
        };
    }

    private static void EnsureOutputFolders(string outputRoot, AppSettings settings)
    {
        Directory.CreateDirectory(Path.Combine(outputRoot, settings.PortraitFolderName));
        Directory.CreateDirectory(Path.Combine(outputRoot, settings.LandscapeFolderName));
        Directory.CreateDirectory(Path.Combine(outputRoot, settings.SquareFolderName));
    }

    private static string? BuildDestPath(string destDir, string srcPath, DuplicateHandling handling)
    {
        var fileName = Path.GetFileName(srcPath);
        var destPath = Path.Combine(destDir, fileName);

        if (!File.Exists(destPath)) return destPath;

        return handling switch
        {
            DuplicateHandling.Overwrite => destPath,
            DuplicateHandling.Skip => null,
            DuplicateHandling.AutoRename => BuildRenamedPath(destDir, srcPath),
            _ => null
        };
    }

    private static string BuildRenamedPath(string destDir, string srcPath)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(srcPath);
        var ext = Path.GetExtension(srcPath);
        int n = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(destDir, $"{nameWithoutExt}_{n}{ext}");
            n++;
        } while (File.Exists(candidate));
        return candidate;
    }

    private static void ExecuteFileOperation(string src, string dest, OperationMode mode)
    {
        try
        {
            if (mode == OperationMode.Copy)
                File.Copy(src, dest, overwrite: true);
            else
                File.Move(src, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, src);
            throw new SortException(src, ex);
        }
    }
}
