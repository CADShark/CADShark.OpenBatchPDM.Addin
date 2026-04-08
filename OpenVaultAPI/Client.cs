using ConsoleHTTP;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CADShark.OpenBatchPDM.Addin.OpenVaultAPI
{
    public partial class Client
        {
            private readonly HttpClient _client;

            public Client()
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
                };

                _client = new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://192.168.1.109:443/")
                };
            }

            // ========= CREATE OBJECT =========
            public async Task<int> CreateObjectAsync(int objectType)
            {
                var url = "api/objects";

                var request = new CreateObjectRequest
                {
                    ObjectType = objectType
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse<CreateObjectResponse>>(resultJson);

                return result.Data.ObjectId;
            }

            // ========= CREATE ATTRIBUTE =========
            public async Task<string> AddAttribute(int objectId, int attributeId, string value)
            {
                var url = $"api/objects/{objectId}/attributes";

                var request = new AttributeRequest
                {
                    AttributeId = attributeId,
                    Value = value
                };



                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }

            // ========= SEARCH OBJECTS =========
            public async Task<int[]> SearchObjectsAsync(SearchRequest request)
            {
                var url = "api/objects/search";

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<ApiResponse<SearchResponse>>(resultJson);

                return result?.Data?.ObjectIds ?? new int[0];
            }

            // ========= UPDATE ATTRIBUTE =========
            public async Task<string> UpdateAttributeAsync(int objectId, AttributeRequest request)
            {
                var url = $"api/objects/{objectId}/attributes";

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync(url, content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }

            // ========= DELETE =========
            public async Task DeleteAttributeAsync(int objectId, int attributeId)
            {
                var url = $"api/objects/{objectId}/attributes/{attributeId}";

                var response = await _client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }

            // ========= GET =========
            public async Task<AttributeResponse> GetAttribute(int objectId, int attributeId)
            {
                var url = $"api/objects/{objectId}/attributes/{attributeId}";

                var response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AttributeResponse>(json);
            }

            // ========= GET BY NAME =========
            public async Task<AttributeResponse> GetAttributeByName(int objectId, string name)
            {
                var url = $"api/objects/{objectId}/attributes/by-name/{name}";

                var response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AttributeResponse>(json);
            }

            // ========= STORAGE (BLOB) =========
            public async Task<string> WritteBlob(string fileName, byte[] fileBody, int objectLinkId, int attributeId, int linkType)
            {
                var url = "api/storage";

                var request = new StorageRequest
                {
                    FileName = fileName,
                    FileBody = Convert.ToBase64String(fileBody), // 🔥 ВАЖНО
                    ObjectLinkId = objectLinkId,
                    AttributeId = attributeId,
                    LinkType = linkType
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

    }

