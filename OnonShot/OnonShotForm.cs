using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Eto.Forms;
using Rhino;

namespace OnonShot
{
    public class OnonShotForm : Dialog
    {
        private readonly Dictionary<string, CheckBox> _checks = new Dictionary<string, CheckBox>();
        private readonly TextBox _folderBox = new TextBox { ReadOnly = true };
        private readonly DropDown _formatBox = new DropDown();
        private readonly CheckBox _matchViewport = new CheckBox { Text = "使用目前視角尺寸", Checked = true };
        private readonly NumericStepper _widthBox = new NumericStepper { MinValue = 16, MaxValue = 16000, Value = 1920, Increment = 10 };
        private readonly NumericStepper _heightBox = new NumericStepper { MinValue = 16, MaxValue = 16000, Value = 1080, Increment = 10 };
        private readonly CheckBox _transparentBox = new CheckBox { Text = "透明背景", Checked = false };
        private readonly Button _exportButton = new Button { Text = "開始匯出" };

        // Rhino 把「跑指令」跟「Eto 對話框事件」視為兩種不同的忙碌狀態：
        // 若在對話框按鈕的 Click 事件裡直接呼叫 RhinoApp.RunScript，指令只會被排入佇列、
        // 不會馬上執行，導致每次擷取畫面時看到的都還是同一個（舊的）場景。
        // 所以這裡的對話框只負責收集設定，實際匯出動作交給對話框關閉後的 OnonShotCommand 執行。
        public bool ExportRequested { get; private set; }
        public List<string> SelectedNames { get; private set; }
        public string OutputFolder => _folderBox.Text;
        public ImageFormat Format => _formatBox.SelectedIndex == 0 ? ImageFormat.Png : ImageFormat.Jpeg;
        public string Extension => _formatBox.SelectedIndex == 0 ? ".png" : ".jpg";
        public bool Transparent => _transparentBox.Checked == true;
        public bool MatchViewport => _matchViewport.Checked == true;
        public int ExportWidth => (int)_widthBox.Value;
        public int ExportHeight => (int)_heightBox.Value;

        private const string UsageText =
            "使用說明\n\n" +
            "1. 先在 Rhino 的 Snapshots 面板（Panels > Snapshots）建立好要輸出的快照。\n" +
            "2. 執行 ononshot 指令開啟這個視窗。\n" +
            "3. 勾選要匯出的快照（可用「全選」／「全不選」快速切換）。\n" +
            "4. 選擇輸出資料夾（會記住上次使用的路徑）。\n" +
            "5. 選擇圖片格式（PNG 可搭配透明背景）與解析度（預設沿用目前視角尺寸）。\n" +
            "6. 按「開始匯出」，視窗會先關閉，Rhino 指令列會依序顯示匯出進度。";

        public OnonShotForm(IEnumerable<string> snapshotNames)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"ononshot v{version.Major}.{version.Minor}.{version.Build} - 批次匯出場景快照";
            Resizable = false;

            _folderBox.Text = OnonShotPlugin.Instance.Settings.GetString("OutputFolder", "");

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
            // DynamicLayout 預設會把「最後一列」拉伸去吃掉多餘空間，導致最後一個項目上方多一大塊空白。
            // 補一列真正的空白列在最後面，讓多餘空間被它吸收，勾選框本身維持自然大小、緊密排列。
            listLayout.AddRow(null);

            var selectAll = new Button { Text = "全選" };
            selectAll.Click += (s, e) => SetAllChecked(true);
            var selectNone = new Button { Text = "全不選" };
            selectNone.Click += (s, e) => SetAllChecked(false);

            var browseButton = new Button { Text = "選擇資料夾..." };
            browseButton.Click += OnBrowseClicked;

            var closeButton = new Button { Text = "關閉" };
            closeButton.Click += (s, e) => Close();
            _exportButton.Click += OnExportClicked;

            var helpButton = new Button { Text = "使用說明" };
            helpButton.Click += (s, e) => MessageBox.Show(this, UsageText, "ononshot", MessageBoxButtons.OK, MessageBoxType.Information);

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

            layout.AddSeparateRow(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items = { helpButton, null, _exportButton, closeButton }
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
            {
                _folderBox.Text = dlg.Directory;
                OnonShotPlugin.Instance.Settings.SetString("OutputFolder", dlg.Directory);
            }
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

            SelectedNames = selected;
            ExportRequested = true;
            Close();
        }
    }
}
