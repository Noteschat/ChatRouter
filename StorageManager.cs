using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatRouter
{
    public class StorageManager
    {
        public static async Task SaveMessage(Client client, ServerMessage message)
        {
            var storageContent = new
            {
                content = message.content,
                version = message.version,
                messageId = message.messageId,
                userId = message.userId
            };

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Cookie", "sessionId=" + client.sessionId);

            var response = await httpClient.PostAsync("http://localhost/api/chat/storage/" + message.chatId, new StringContent(JsonSerializer.Serialize(storageContent), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn(response.ReasonPhrase);
            }
        }

        public static async Task<Either<Chat, ChatError>> GetChat(Client client, string id)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Cookie", "sessionId=" + client.sessionId);

            var response = await httpClient.GetAsync("http://localhost/api/chat/storage/" + id);
            if (!response.IsSuccessStatusCode)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        return new Either<Chat, ChatError>(ChatError.Unauthorized);
                    case HttpStatusCode.NotFound:
                        return new Either<Chat, ChatError>(ChatError.NotFound);
                    case HttpStatusCode.InternalServerError:
                        return new Either<Chat, ChatError>(ChatError.ServerError);
                }
            }
            try
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var chat = JsonSerializer.Deserialize<Chat>(jsonResponse);
                return new Either<Chat, ChatError>(chat);
            }
            catch
            {
                return new Either<Chat, ChatError>(ChatError.FormatError);
            }
        }
    }

    public struct Chat
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("users")]
        public string[] Users { get; set; }
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; }
    }

    public struct ChatMessage
    {
        [JsonPropertyName("messageId")]
        public string MessageId { get; set; }
        [JsonPropertyName("version")]
        public int Version { get; set; }
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public enum ChatError
    {
        Unauthorized,
        NotFound,
        ServerError,
        FormatError
    }
}
