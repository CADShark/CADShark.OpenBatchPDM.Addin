using Newtonsoft.Json;

namespace CADShark.OpenBatchPDM.Addin.OpenVaultAPI
{

    public class CreateObjectResponse
    {
        [JsonProperty("objectId")]
        public int ObjectId { get; set; }

        [JsonProperty("versionId")]
        public int VersionId { get; set; }

        [JsonProperty("objectType")]
        public int ObjectType { get; set; }
    }
}