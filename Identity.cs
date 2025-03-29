using System.Text.Json.Serialization;

namespace ChatRouter
{
    public struct User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
