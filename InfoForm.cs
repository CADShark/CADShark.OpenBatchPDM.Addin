using CADShark.Common.Analytics;
using System.Windows.Forms;

namespace CADShark.OpenBatchPDM.Addin
{
    public partial class InfoForm : Form
    {
        public InfoForm()
        {
            InitializeComponent();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://t.me/DenisOrel");
            Telemetry.LogButtonClick("Void link to Telegram");
        }

        private void CloseButton_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://sites.google.com/view/cad-shark/");
            Telemetry.LogButtonClick("Void link to wbb-site");
        }
    }
}
