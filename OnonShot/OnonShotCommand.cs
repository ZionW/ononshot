using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace OnonShot
{
    public class OnonShotCommand : Command
    {
        public OnonShotCommand()
        {
            Instance = this;
        }

        public static OnonShotCommand Instance { get; private set; }

        public override string EnglishName => "ononshot";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var names = new List<string>(doc.Snapshots.Names);
            if (names.Count == 0)
            {
                RhinoApp.WriteLine("這個檔案裡沒有任何 Snapshot，請先用 Rhino 的 Snapshots 面板（Panels > Snapshots）建立快照。");
                return Result.Nothing;
            }

            bool exportRequested;
            List<string> selected;
            string outputFolder;
            System.Drawing.Imaging.ImageFormat format;
            string extension;
            bool transparent, matchViewport;
            int width, height;

            using (var form = new OnonShotForm(names))
            {
                form.ShowModal(RhinoEtoApp.MainWindowForDocument(doc));
                exportRequested = form.ExportRequested;
                selected = form.SelectedNames;
                outputFolder = form.OutputFolder;
                format = form.Format;
                extension = form.Extension;
                transparent = form.Transparent;
                matchViewport = form.MatchViewport;
                width = form.ExportWidth;
                height = form.ExportHeight;
            }

            if (!exportRequested) return Result.Success;

            // RhinoApp.RunScript 不能在指令執行中（也就是這個 RunCommand 本身還沒 return）同步跑，
            // 否則會被 Rhino 排入佇列延後執行，導致每張擷取到的都還是同一個舊場景。
            // 所以這裡讓 ononshot 指令先正常結束，實際的還原＋擷取工作等到 Rhino 閒置（Idle）後才真正執行。
            EventHandler idleHandler = null;
            idleHandler = (s, e) =>
            {
                RhinoApp.Idle -= idleHandler;
                RunExport(doc, selected, outputFolder, format, extension, transparent, matchViewport, width, height);
            };
            RhinoApp.Idle += idleHandler;

            return Result.Success;
        }

        private static void RunExport(RhinoDoc doc, List<string> selected, string outputFolder,
            System.Drawing.Imaging.ImageFormat format, string extension, bool transparent, bool matchViewport,
            int width, int height)
        {
            var view = doc.Views.ActiveView;
            var failed = new List<string>();
            var done = 0;

            foreach (var name in selected)
            {
                done++;
                RhinoApp.WriteLine($"[ononshot] 正在匯出 ({done}/{selected.Count})：{name}");

                try
                {
                    RestoreSnapshot(doc, name);
                    RhinoApp.WriteLine($"[ononshot]   還原後相機位置：{view.ActiveViewport.CameraLocation}");

                    var w = matchViewport ? view.ActiveViewport.Size.Width : width;
                    var h = matchViewport ? view.ActiveViewport.Size.Height : height;

                    var capture = new Rhino.Display.ViewCapture
                    {
                        Width = w,
                        Height = h,
                        ScaleScreenItems = false,
                        DrawAxes = false,
                        DrawGridAxes = false,
                        DrawGrid = false,
                        TransparentBackground = transparent
                    };

                    using (var bmp = capture.CaptureToBitmap(view))
                    {
                        var path = Path.Combine(outputFolder, SanitizeFileName(name) + extension);
                        bmp.Save(path, format);
                    }
                }
                catch (Exception ex)
                {
                    failed.Add($"{name}（{ex.Message}）");
                }
            }

            RhinoApp.WriteLine(failed.Count == 0
                ? $"[ononshot] 完成！已匯出 {selected.Count} 張圖片。"
                : $"[ononshot] 完成，但有 {failed.Count} 個失敗：{string.Join("; ", failed)}");
        }

        private static void RestoreSnapshot(RhinoDoc doc, string name)
        {
            // "_Cancel" 結尾是為了乾淨跳出 Snapshots 面板本身會停留的選項迴圈；
            // 因為是用取消離開，RunScript 的回傳值一定是 false，即使還原已經成功，所以這裡不檢查它。
            var escaped = name.Replace("\"", "");
            RhinoApp.RunScript($"-Snapshots _Restore \"{escaped}\" _Enter _Cancel", false);
            doc.Views.Redraw();
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
