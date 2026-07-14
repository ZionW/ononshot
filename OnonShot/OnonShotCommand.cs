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
            var names = new System.Collections.Generic.List<string>(doc.Snapshots.Names);
            if (names.Count == 0)
            {
                RhinoApp.WriteLine("這個檔案裡沒有任何 Snapshot，請先用 Rhino 的 Snapshots 面板（Panels > Snapshots）建立快照。");
                return Result.Nothing;
            }

            using (var form = new OnonShotForm(doc, names))
            {
                form.ShowModal(RhinoEtoApp.MainWindowForDocument(doc));
            }

            return Result.Success;
        }
    }
}
