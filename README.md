# ononshot

Rhino 8 外掛：批次把檔案裡的 Snapshots（快照，Panels > Snapshots 面板）逐一還原，並把目前作用中的視角擷取成圖片。

A Rhino 8 plug-in that batch-restores every Snapshot in the current document (Panels > Snapshots) and captures the active viewport to an image for each one.

![ononshot](ononshot.png)

## 使用方式 / Usage

1. 先用 Rhino 的 Snapshots 面板（Panels > Snapshots）建立好要輸出的快照。
   Create the snapshots you want to export using Rhino's Snapshots panel (Panels > Snapshots) first.
2. 執行指令 `ononshot`。
   Run the `ononshot` command.
3. 在跳出的視窗中勾選要匯出的快照、選擇輸出資料夾（會記住上次路徑）、圖片格式（PNG/JPG）、解析度（預設沿用目前視角尺寸）與是否透明背景（僅 PNG）。
   In the dialog, check the snapshots to export, pick an output folder (remembered from last time), image format (PNG/JPG), resolution (defaults to the current viewport size), and optional transparent background (PNG only).
4. 按「開始匯出」。視窗會先關閉，接著 Rhino 指令列會依序印出進度（例如 `[ononshot] 正在匯出 (2/5)：快照 02`），外掛依序還原每個快照並擷取目前作用中的視角另存成同名圖片。
   Click "開始匯出" (Start Export). The dialog closes first, then the Rhino command line prints progress for each snapshot (e.g. `[ononshot] 正在匯出 (2/5)：快照 02`) as it restores each snapshot and saves the active view to a matching-name image file.

視窗右下角的「使用說明」按鈕也有同一份操作步驟。
The "使用說明" (Usage) button in the dialog shows the same steps.

## 安裝 / Install

- 到 [Releases](https://github.com/ZionW/ononshot/releases) 或自行建置（見下方），把 `.rhp` 拖進 Rhino 視窗，或用 `.yak` 透過 `_PackageManager` 安裝。
  Grab a build from [Releases](https://github.com/ZionW/ononshot/releases) or build it yourself (see below), then drag the `.rhp` into a Rhino viewport, or install the `.yak` via `_PackageManager`.
- 同一個 `.yak` 套件同時包含 Mac（`net7.0`）與 Windows（`net48` + `net7.0`）兩種執行檔，Mac／Windows 都能直接安裝使用。
  The same `.yak` package bundles both the Mac (`net7.0`) and Windows (`net48` + `net7.0`) builds, so it installs directly on either platform.

## 開發 / Development

在 `OnonShot/` 目錄下執行 `dotnet build`：多目標建置 `net7.0`（Mac／現代 Windows Rhino 8）與 `net48`（Rhino 7／傳統 Windows Rhino 8），並自動打包成單一 `.yak`（需本機安裝 Rhino 8 以取得 `yak` 執行檔）。

Run `dotnet build` inside `OnonShot/`: it multi-targets `net7.0` (Mac / modern Windows Rhino 8) and `net48` (Rhino 7 / classic Windows Rhino 8), and packages both into a single `.yak` (requires a local Rhino 8 install for the `yak` executable).

## 技術重點 / Technical notes

- 快照還原是透過 `-Snapshots _Restore "{name}" _Enter _Cancel` 巨集指令觸發的，因為 RhinoCommon 的 `SnapshotTable` 只公開 `Names`（讀取快照清單），沒有公開的還原 API；選項名稱要加底線前綴（`_Restore`）才能跨語系正確識別。
  Snapshot restore is driven via the `-Snapshots _Restore "{name}" _Enter _Cancel` macro, since RhinoCommon's `SnapshotTable` only exposes `Names` (reading the snapshot list) — there's no public restore API. Option keywords need the underscore prefix (`_Restore`) to resolve correctly regardless of Rhino's UI language.
- 還原＋擷取的迴圈是在 `RhinoApp.Idle`事件裡執行，而不是直接寫在指令的 `RunCommand`（或對話框按鈕事件）裡：`RhinoApp.RunScript` 若在指令執行中或 Eto 對話框事件裡被呼叫，會被 Rhino 排入佇列延後執行、不會同步跑完，導致每張擷取到的都還是同一個舊場景。
  The restore-and-capture loop runs inside a `RhinoApp.Idle` handler rather than directly in the command's `RunCommand` (or a dialog button click): calling `RhinoApp.RunScript` while a command is already running, or from inside an Eto dialog event, gets queued by Rhino instead of executing synchronously — so every capture would otherwise see the same stale scene.
