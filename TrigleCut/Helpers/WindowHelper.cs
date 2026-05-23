using Microsoft.UI.Windowing;
using Windows.Graphics;
using TrigleCut.Models;

namespace TrigleCut.Helpers;

public static class WindowHelper
{
    public static void RestorePosition(Microsoft.UI.Xaml.Window window, AppSettings settings)
    {
        var appWin = window.AppWindow;

        if (settings.WindowWidth <= 0 || settings.WindowHeight <= 0) return;

        int w = Math.Max(settings.WindowWidth, 600);
        int h = Math.Max(settings.WindowHeight, 700);
        int x = settings.WindowX;
        int y = settings.WindowY;

        if (x < 0 && y < 0)
        {
            // 未保存の場合は画面中央に配置
            var primary = DisplayArea.Primary;
            x = (primary.WorkArea.Width - w) / 2 + primary.WorkArea.X;
            y = (primary.WorkArea.Height - h) / 2 + primary.WorkArea.Y;
        }
        else if (!IsTitleBarVisible(x, y, w))
        {
            // 保存済み位置がどのモニターにも表示されない場合はプライマリに移動
            var primary = DisplayArea.Primary;
            x = (primary.WorkArea.Width - w) / 2 + primary.WorkArea.X;
            y = (primary.WorkArea.Height - h) / 2 + primary.WorkArea.Y;
        }

        appWin.MoveAndResize(new RectInt32 { X = x, Y = y, Width = w, Height = h });
    }

    public static void SavePosition(Microsoft.UI.Xaml.Window window, AppSettings settings)
    {
        var appWin = window.AppWindow;
        if (appWin.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized })
            return; // 最大化時は保存しない

        settings.WindowX = appWin.Position.X;
        settings.WindowY = appWin.Position.Y;
        settings.WindowWidth = appWin.Size.Width;
        settings.WindowHeight = appWin.Size.Height;
    }

    // タイトルバー領域（上端から50px幅）がいずれかのモニターに50px以上かかっているか確認
    private static bool IsTitleBarVisible(int x, int y, int w)
    {
        const int TitleBarHeight = 50;
        const int MinVisibleWidth = 50;

        // foreach は WinRT IReadOnlyList<T> の列挙で InvalidCastException を起こすため
        // インデックスアクセスを使用する
        var areas = DisplayArea.FindAll();
        for (int i = 0; i < areas.Count; i++)
        {
            var wa = areas[i].WorkArea;
            bool verticallyVisible = y >= wa.Y - TitleBarHeight && y < wa.Y + wa.Height;
            bool horizontallyVisible = x + w > wa.X + MinVisibleWidth && x < wa.X + wa.Width - MinVisibleWidth;
            if (verticallyVisible && horizontallyVisible) return true;
        }
        return false;
    }
}
