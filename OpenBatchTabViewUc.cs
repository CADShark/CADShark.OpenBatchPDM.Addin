using CADShark.Common.DBManager.Data;
//using Spire.PdfViewer.Forms;
using System.IO;
using System.Linq;
using System.Windows.Forms;


namespace CADShark.OpenBatchPDM.AddIn
{
    public partial class OpenBatchTabViewUc : UserControl
    {
        //private readonly PdfViewer _viewer;
        public OpenBatchTabViewUc()
        {
            InitializeComponent();
            //_viewer = new PdfViewer();
        }

        //public void ShowPdf(string connectionString, int id, int localVersion)
        //{
        //    var folderPath = GetPdfFile(connectionString, id, localVersion);
        //    if (folderPath == null) return;
        //    // Create PDF Document
        //    this.Controls.Add(_viewer);
        //    _viewer.Dock = DockStyle.Fill;
        //    _viewer.LoadFromStream(new MemoryStream(folderPath));
        //}

        //public string GetPdfFile(string connectionString, int id, int localVersion)
        //{
        //    var folderPath = string.Empty;
        //    var db = new AddDbContext(connectionString);
        //    var objectPdf = db.Items.FirstOrDefault(item =>
        //        item.DocumentId == id && item.Version == localVersion && item.DocType == "PDF");

        //    if (objectPdf == null) return folderPath;

        //    folderPath = Path.Combine(Path.GetTempPath(), objectPdf.FileName);
        //    File.WriteAllBytes(folderPath, objectPdf.Blob);

        //    return folderPath;
        //}

        //public byte[] GetPdfFile(string connectionString, int id, int localVersion)
        //{
        //    var folderPath = string.Empty;
        //    var db = new AddDbContext(connectionString);
        //    var objectPdf = db.Items.FirstOrDefault(item =>
        //            item.DocumentId == id && item.Version == localVersion && item.DocType == "PDF")
        //        ?.Blob;
        //    return objectPdf;
        //}

        //public void Close()
        //{
        //    this.Controls.Remove(_viewer);
        //}
    }
}