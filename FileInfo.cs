namespace CADShark.OpenBatchPDM.Addin
{
    public class FileInfo
    {
        public int DocumentId { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Revision { get; set; }
        public int CurrentVersion { get; set; }
        //public string DrawPath { get; set; }
        public int FolderId { get; set; }
    }
}
