using Newtonsoft.Json;

namespace CADShark.OpenBatchPDM.Addin.OpenVaultAPI
{

    public class CreateObjectRequest
    {
        [JsonProperty("objectType")]
        public int ObjectType { get; set; }
    }


    public class SearchRequest
    {
        public Filter[] Filters { get; set; }
    }

    public class Filter
    {
        public int AttributeId { get; set; }
        public string Value { get; set; }
    }

    public class SearchResponse
    {
        public int[] ObjectIds { get; set; }
    }
}