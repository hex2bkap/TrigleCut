namespace PicOrient.Models;

public enum DuplicateHandling { Skip, Overwrite, AutoRename }
public enum OperationMode { Copy, Move }

public class AppSettings
{
    // フォルダ名
    public string PortraitFolderName { get; set; } = "縦";
    public string LandscapeFolderName { get; set; } = "横";
    public string SquareFolderName { get; set; } = "中間";

    // 操作設定
    public OperationMode Operation { get; set; } = OperationMode.Copy;
    public DuplicateHandling DuplicateHandling { get; set; } = DuplicateHandling.Skip;
    public double AspectRatioThreshold { get; set; } = 1.1;
    public bool ProcessSubfolders { get; set; } = false;

    // 出力先
    public bool UseSourceFolderAsOutput { get; set; } = true;
    public string CustomOutputFolder { get; set; } = "";

    // 最後に使用したフォルダ
    public string LastSourceFolder { get; set; } = "";

    // ウィンドウ状態
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = 720;
    public int WindowHeight { get; set; } = 860;
}
