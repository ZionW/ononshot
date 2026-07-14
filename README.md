# ononshot

Rhino 8 外掛：批次把檔案裡的 Snapshots（快照面板 Panels > Snapshots）逐一還原，並把目前作用中的視角擷取成圖片。

## 使用方式

1. 先用 Rhino 的 Snapshots 面板（Panels > Snapshots）建立好要輸出的快照。
2. 執行指令 `ononshot`。
3. 在跳出的視窗中勾選要匯出的快照、選擇輸出資料夾、圖片格式（PNG/JPG）、解析度（預設沿用目前視角尺寸）與是否透明背景（僅 PNG）。
4. 按「開始匯出」，外掛會依序還原每個快照並擷取目前作用中的視角另存成同名圖片。

## 開發

- `dotnet build` 於 `OnonShot/` 目錄下建置（`net7.0` for Mac/現代 Windows Rhino 8，`net48` for Rhino 7 / 傳統 Windows Rhino 8），一併打包 `.yak`（需本機安裝 Rhino 8 以取得 `yak` 執行檔）。
- 安裝：把建置出的 `.rhp` 拖進 Rhino，或用 `.yak` 透過 `_PackageManager` 安裝。

## 已知限制

- 快照還原是透過 `-Snapshots Restore "{name}" _Enter _Enter` 巨集指令觸發（RhinoCommon 沒有公開穩定的「還原」API，改用巨集是 McNeel 論壇上確認過可行的做法）。這段巨集語法未在真實 Rhino 環境測試過——第一次執行時請留意 Rhino 指令列有無錯誤訊息；若還原失敗，多半是這行巨集語法要微調。
