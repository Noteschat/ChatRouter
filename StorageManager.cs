using System.Text;
using System.Text.Json;

namespace ChatRouter
{
    public class StorageManager
    {
        public static async Task SaveMessage(Client client, string message, string chatId)
        {
            var lines = message.Split('\n');
            var content = string.Join("\n", lines, 2, lines.Length - 2);
            var version = int.Parse(lines[1]);
            var msgId = lines[0];

            var storageContent = new
            {
                content,
                version,
                messageId = msgId,
                userId = client.user.Value.Id
            };

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Cookie", "sessionId=" + client.sessionId);

            var response = await httpClient.PostAsync("http://localhost/api/chat/storage/" + chatId, new StringContent(JsonSerializer.Serialize(storageContent), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn(response.ReasonPhrase);
            }
        }
    }
}
