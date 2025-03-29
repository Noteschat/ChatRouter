using System.Text;
using System.Text.Json;

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
    }
}
