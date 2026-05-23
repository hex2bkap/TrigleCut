# 開発ガイド

このドキュメントは、TrigleCut の開発プロセス・技術的判断・注意事項をまとめたものです。
同様のWinUI 3アプリを開発する際の参考としても使えます。

---

## プロジェクト概要

| 項目 | 内容 |
|------|------|
| 言語 | C# 12 / .NET 8 |
| UIフレームワーク | WinUI 3（Windows App SDK 2.1.3） |
| MVVMライブラリ | CommunityToolkit.Mvvm 8.4.2 |
| パッケージ形式 | Unpackaged（MSIX不使用） |
| 最小OS | Windows 10 1809 |

---

## 開発環境セットアップ

1. Visual Studio 2022 または VS Code + C# Dev Kit
2. .NET 8 SDK
3. Windows App SDK 2.x ワークロード（`winget install Microsoft.WindowsAppSDK`）

ビルド確認:
```
cd TrigleCut
dotnet build
```

---

## ビルドと実行

### デバッグ実行
```
dotnet run --project TrigleCut/TrigleCut.csproj
```

### 配布用ビルド（self-contained, x64）
```
dotnet publish TrigleCut/TrigleCut.csproj -c Release -r win-x64 -o publish/
```
出力先 `publish/` フォルダを zip にして配布する。

---

## プロジェクト構成

```
TrigleCut/
├── App.xaml / App.xaml.cs          # アプリケーションエントリポイント
├── MainWindow.xaml / .xaml.cs      # メインウィンドウ（View）
├── Program.cs                      # エントリポイント手動定義（※1）
├── Models/
│   └── AppSettings.cs              # 設定データモデル
├── ViewModels/
│   └── MainViewModel.cs            # ビジネスロジック・状態管理
├── Services/
│   ├── ImageScannerService.cs      # フォルダスキャン・画像判定
│   ├── ImageSorterService.cs       # ファイルのコピー/移動
│   ├── SettingsService.cs          # 設定の読み書き（JSON）
│   └── LogService.cs               # ログ出力
└── Helpers/
    └── WindowHelper.cs             # ウィンドウ位置の保存・復元
```

---

## 開発フロー

### 採用したアプローチ

1. **MVP優先** — まず動くものを作り、UXや細部は後から改善
2. **実機テスト重視** — ビルドして実際に動かしながら確認。自動テストは未導入
3. **段階的なUX改善** — 動作確認 → 使いにくい点を洗い出し → 改善 のサイクル
4. **バグは再現してから直す** — 症状だけで直さず、ログやデバッグ出力で原因を特定してから修正

### 機能追加時の手順

1. ViewModelにプロパティ/コマンドを追加（`[ObservableProperty]`, `[RelayCommand]`）
2. XAMLにバインディングを追加
3. ビルドして動作確認
4. 問題があればログを見て原因特定

---

## コーディング規約

- MVVM パターン厳守：View（XAML）にロジックを書かない
- `[ObservableProperty]` でプロパティ自動生成（CommunityToolkit.Mvvm）
- 計算プロパティ（Visibility等）は `[NotifyPropertyChangedFor(nameof(...))]` で連動
- コメントは「なぜ」が自明でない箇所のみ記述

---

## テスト方針

自動テストは未導入。以下の手動テスト項目を確認すること：

### スキャン
- [ ] 縦・横・中間が混在するフォルダ
- [ ] サブフォルダあり / なし
- [ ] EXIF回転付き写真（スマホ縦撮りJPEG）
- [ ] HEIC/HEIF（HEIFコーデックが入っているPCで確認）
- [ ] キャンセル動作

### 振り分け
- [ ] コピー / 移動
- [ ] 重複ファイル：スキップ / 上書き / 自動リネーム
- [ ] 出力先：ソースフォルダ内 / 別フォルダ指定
- [ ] フォルダ名カスタマイズ
- [ ] エラーが発生した場合のInfoBar表示
- [ ] キャンセル動作

### エラーテストの方法
出力先ファイルを読み取り専用に設定し、「上書き」モードで振り分けを実行すると `UnauthorizedAccessException` が発生し、InfoBarにエラーが表示される。

---

## 既知の問題・注意事項

### WinRT IReadOnlyList\<T\> の foreach バグ
`DisplayArea.FindAll()` を `foreach` で列挙すると `InvalidCastException` が発生する。
Windows App SDK 2.1.3 の既知の問題。インデックスアクセスで回避すること。

```csharp
// NG
foreach (var area in DisplayArea.FindAll()) { ... }

// OK
var areas = DisplayArea.FindAll();
for (int i = 0; i < areas.Count; i++) { var area = areas[i]; ... }
```

### GetImagePropertiesAsync() はEXIF補正済みの表示サイズを返す
`StorageFile.Properties.GetImagePropertiesAsync()` の `Width`/`Height` は
EXIF回転を適用済みの**表示サイズ**を返す。
`System.Photo.Orientation` を読んで手動でスワップすると二重補正になる。

```csharp
// GetImagePropertiesAsync() だけで OK。手動スワップは不要。
var props = await file.Properties.GetImagePropertiesAsync();
uint width = props.Width;   // すでに回転補正済み
uint height = props.Height;
```

### Progress\<T\> の非同期性によるレースコンディション
`Progress<T>` はコールバックを `SynchronizationContext.Post`（非同期）でUIスレッドにポストする。
処理完了後に最後のコールバックが発火して完了メッセージを上書きする問題が発生した。
`IsProcessing = false` をセット後にコールバックが実行されることを利用し、
コールバック内の先頭で `if (!IsProcessing) return;` ガードを入れて解決。

### WinUI 3 のカスタムボタンスタイル
`Style` の `Setter Property="Resources"` は WinUI 3 では使用不可（WMC0095エラー）。
ボタンの色を変えるには各 `Button` 要素の `<Button.Resources>` でリソースキーを上書きする。

```xml
<Button>
    <Button.Resources>
        <SolidColorBrush x:Key="ButtonBackground" Color="Transparent"/>
        <SolidColorBrush x:Key="ButtonForeground" Color="#FF6B6B"/>
        ...
    </Button.Resources>
</Button>
```

### WinUI 3 の Program.cs
`App.g.cs` にエントリポイントが自動生成されないため、`Program.cs` を手動作成し、
`.csproj` に `<DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>` を設定する。

### Windows App SDK ランタイムの必要性
`WindowsPackageType=None`（unpackaged）では Windows App SDK ランタイムが別途必要。
配布時は受け取り側に `WindowsAppRuntimeInstall-x64.exe` のインストールを案内すること。

---

## 設定・ログの保存場所

```
%LocalAppData%\TrigleCut\
├── settings.json    # アプリ設定
└── logs\
    └── app-YYYY-MM-DD.log
```

---

## リリース手順

1. `MainWindow.xaml.cs` の `AppVersion` を更新
2. `TrigleCut.csproj` の `<Version>` を更新
3. `README.md` のバージョン履歴を追記
4. ビルドと動作確認
5. git commit + tag（例: `git tag v1.1.0`）
6. publish して zip を作成:
   ```
   dotnet publish TrigleCut/TrigleCut.csproj -c Release -r win-x64 -o publish/
   ```
7. `TrigleCut-vX.X.X.zip` にまとめて配布

---

## TODO（次のバージョンに向けて）

- [ ] アプリ名確定・アイコン作成
- [ ] ウィンドウ最小サイズ設定
- [ ] ログの自動ローテーション（古いファイルを削除）
- [ ] SmartScreen警告への対処（README注記 or コードサイニング）
- [ ] HEIC動作確認（HEIFコーデック入りPCで）
- [ ] リサイズ機能
- [ ] GitHub公開（スクリーンショット・LICENSEファイル追加）
