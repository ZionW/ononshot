using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Eto.Forms;
using Rhino;

namespace OnonShot
{
    public class OnonShotForm : Dialog
    {
        private readonly RhinoDoc _doc;
        private readonly Dictionary<string, CheckBox> _checks = new Dictionary<string, CheckBox>();
        private readonly TextBox _folderBox = new TextBox { ReadOnly = true };
        private readonly DropDown _formatBox = new DropDown();
        private readonly CheckBox _matchViewport = new CheckBox { Text = "使用目前視角尺寸", Checked = true };
        private readonly NumericStepper _widthBox = new NumericStepper { MinValue = 16, MaxValue = 16000, Value = 1920, Increment = 10 };
        private readonly NumericStepper _heightBox = new NumericStepper { MinValue = 16, MaxValue = 16000, Value = 1080, Increment = 10 };
        private readonly CheckBox _transparentBox = new CheckBox { Text = "透明背景", Checked = false };
        private readonly Label _statusLabel = new Label { Text = "" };
        private readonly Button _exportButton = new Button { Text = "開始匯出" };

        public OnonShotForm(RhinoDoc doc, IEnumerable<string> snapshotNames)
        {
            _doc = doc;
            Title = "ononshot - 批次匯出場景快照";
            Resizable = false;

            _formatBox.Items.Add("PNG");
            _formatBox.Items.Add("JPG");
            _formatBox.SelectedIndex = 0;
            _formatBox.SelectedIndexChanged += (s, e) => UpdateTransparentAvailability();

            _matchViewport.CheckedChanged += (s, e) => UpdateSizeFieldsEnabled();
            UpdateSizeFieldsEnabled();
            UpdateTransparentAvailability();

            var listLayout = new DynamicLayout { Spacing = new Eto.Drawing.Size(6, 4), Padding = new Eto.Drawing.Padding(0, 4) };
            foreach (var name in snapshotNames)
            {
                var cb = new CheckBox { Text = name, Checked = true };
                _checks[name] = cb;
                listLayout.AddRow(cb);
            }

            var selectAll = new Button { Text = "全選" };
            selectAll.Click += (s, e) => SetAllChecked(true);
            var selectNone = new Button { Text = "全不選" };
            selectNone.Click += (s, e) => SetAllChecked(false);

            var browseButton = new Button { Text = "選擇資料夾..." };
            browseButton.Click += OnBrowseClicked;

            var closeButton = new Button { Text = "關閉" };
            closeButton.Click += (s, e) => Close();
            _exportButton.Click += OnExportClicked;

            var layout = new DynamicLayout { Padding = 12, Spacing = new Eto.Drawing.Size(8, 8) };

            layout.AddRow(new Label { Text = $"共 {_checks.Count} 個快照：" });
            layout.AddRow(new Scrollable { Content = listLayout, Height = 200, Width = 260 });
            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { selectAll, selectNone }
            });

            layout.AddSeparateRow(new Label { Text = "輸出資料夾：" });
            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { new StackLayoutItem(_folderBox, true), browseButton }
            });

            layout.AddSeparateRow(new Label { Text = "圖片格式：" }, _formatBox, null);
            layout.AddRow(_matchViewport);
            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { new Label { Text = "寬:" }, _widthBox, new Label { Text = "高:" }, _heightBox }
            });
            layout.AddRow(_transparentBox);

            layout.AddSeparateRow(_statusLabel);
            layout.AddRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { null, _exportButton, closeButton }
            });

            Content = layout;
        }

        private void SetAllChecked(bool value)
        {
            foreach (var cb in _checks.Values) cb.Checked = value;
        }

        private void UpdateSizeFieldsEnabled()
        {
            var enabled = _matchViewport.Checked != true;
            _widthBox.Enabled = enabled;
            _heightBox.Enabled = enabled;
        }

        private void UpdateTransparentAvailability()
        {
            var isPng = _formatBox.SelectedIndex == 0;
            _transparentBox.Enabled = isPng;
            if (!isPng) _transparentBox.Checked = false;
        }

        private void OnBrowseClicked(object sender, EventArgs e)
        {
            var dlg = new SelectFolderDialog();
            if (!string.IsNullOrEmpty(_folderBox.Text)) dlg.Directory = _folderBox.Text;
            if (dlg.ShowDialog(this) == DialogResult.Ok)
                _folderBox.Text = dlg.Directory;
        }

        private void OnExportClicked(object sender, EventArgs e)
        {
            var selected = _checks.Where(kv => kv.Value.Checked == true).Select(kv => kv.Key).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "請至少選擇一個快照。", "ononshot", MessageBoxButtons.OK, MessageBoxType.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_folderBox.Text) || !Directory.Exists(_folderBox.Text))
            {
                MessageBox.Show(this, "請先選擇一個存在的輸出資料夾。", "ononshot", MessageBoxButtons.OK, MessageBoxType.Warning);
                return;
            }

            _exportButton.Enabled = false;
            var format = _formatBox.SelectedIndex == 0 ? ImageFormat.Png : ImageFormat.Jpeg;
            var ext = _formatBox.SelectedIndex == 0 ? ".png" : ".jpg";
            var transparent = _transparentBox.Checked == true;
            var matchViewport = _matchViewport.Checked == true;
            var width = (int)_widthBox.Value;
            var height = (int)_heightBox.Value;

            var view = _doc.Views.ActiveView;
            var failed = new List<string>();
            var done = 0;

            foreach (var name in selected)
            {
                done++;
                _statusLabel.Text = $"正在匯出 ({done}/{selected.Count})：{name}";
                Application.Instance.RunIteration();

                try
                {
                    RestoreSnapshot(name);

                    var w = matchViewport ? view.ActiveViewport.Size.Width : width;
                    var h = matchViewport ? view.ActiveViewport.Size.Height : height;

                    var capture = new Rhino.Display.ViewCapture
                    {
                        Width = w,
                        Height = h,
                        ScaleScreenItems = false,
                        DrawAxes = false,
                        DrawGrid = false,
                        DrawGridAxes = false,
                        TransparentBackground = transparent
                    };

                    using (var bmp = capture.CaptureToBitmap(view))
                    {
                        var path = Path.Combine(_folderBox.Text, SanitizeFileName(name) + ext);
                        bmp.Save(path, format);
                    }
                }
                catch (Exception ex)
                {
                    failed.Add($"{name}（{ex.Message}）");
                }
            }

            _statusLabel.Text = failed.Count == 0
                ? $"完成！已匯出 {selected.Count} 張圖片。"
                : $"完成，但有 {failed.Count} 個失敗：{string.Join("; ", failed)}";
            _exportButton.Enabled = true;
        }

        private void RestoreSnapshot(string name)
        {
            var escaped = name.Replace("\"", "");
            var ok = RhinoApp.RunScript($"-Snapshots Restore \"{escaped}\" _Enter _Enter", false);
            if (!ok) throw new InvalidOperationException("還原快照失敗");
            _doc.Views.Redraw();
            Application.Instance.RunIteration();
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
