using System.IO;

namespace CADShark.OpenBatchPDM.Addin
{
    internal static class BlobReader
    {
        public static byte[] ReadAllBytes(string filePath)
        {
            byte[] fileBytes = null;

            if (File.Exists(filePath))
                fileBytes = File.ReadAllBytes(filePath);
            return fileBytes;
        }
    }
}
