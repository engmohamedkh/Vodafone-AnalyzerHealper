using System;
using System.Windows.Forms;

namespace AnalyzerHelper
{
    public class OptionsForm : Form
    {
        public bool CleanDefaultsChecked => chkDefaults.Checked;
        public bool UpdateAnnotationsChecked => chkAnnotations.Checked;

        private CheckBox chkDefaults;
        private CheckBox chkAnnotations;
        private Button btnOk;
        private Button btnCancel;

        public OptionsForm()
        {
            Text = "Select Options";
            Width = 300;
            Height = 180;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            chkDefaults = new CheckBox { Text = "Clean Default Values", Left = 20, Top = 20, Width = 200, Checked = true };
            chkAnnotations = new CheckBox { Text = "Update Annotations", Left = 20, Top = 50, Width = 200, Checked = true };

            btnOk = new Button { Text = "OK", Left = 50, Width = 80, Top = 90, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 150, Width = 80, Top = 90, DialogResult = DialogResult.Cancel };

            Controls.Add(chkDefaults);
            Controls.Add(chkAnnotations);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
