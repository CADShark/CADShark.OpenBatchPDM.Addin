namespace CADShark.OpenBatchPDM.Addin.OpenVaultAPI
{
    public partial class Client
    {
        public class StorageRequest
        {
            public string FileName { get; set; }
            public string FileBody { get; set; } // base64!
            public int ObjectLinkId { get; set; }
            public int AttributeId { get; set; }
            public int LinkType { get; set; }
        }
    }
}