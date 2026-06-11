using System.Text;
using System.Text.Json;

namespace LunaCore.Api.Services;

/// <summary>Envía mensajes por WhatsApp Cloud API (Meta Graph). Requiere WhatsApp:AccessToken + WhatsApp:PhoneNumberId.</summary>
public class WhatsAppService(IHttpClientFactory httpFactory, IConfiguration cfg)
{
    public bool Configured =>
        !string.IsNullOrWhiteSpace(cfg["WhatsApp:AccessToken"]) &&
        !string.IsNullOrWhiteSpace(cfg["WhatsApp:PhoneNumberId"]);

    public async Task SendAsync(string to, string text)
    {
        var token = cfg["WhatsApp:AccessToken"];
        var phoneId = cfg["WhatsApp:PhoneNumberId"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneId)) return;

        var payload = new { messaging_product = "whatsapp", to, type = "text", text = new { body = text } };
        var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        await http.PostAsync($"https://graph.facebook.com/v21.0/{phoneId}/messages",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    }
}
