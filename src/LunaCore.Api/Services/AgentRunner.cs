using System.Text;
using System.Text.Json;
using LunaCore.Api.Models;

namespace LunaCore.Api.Services;

public record AgentResult(bool Ok, string? Reply, string? Error);

/// <summary>Llama a Groq con el cerebro del agente. Compartido por el panel (/api/chat) y WhatsApp.</summary>
public class AgentRunner(IHttpClientFactory httpFactory, IConfiguration cfg)
{
    public async Task<AgentResult> ReplyAsync(Negocio negocio, AgenteConfig agente, List<(string role, string content)> history)
    {
        var key = cfg["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) return new(false, null, "config");

        var model = cfg["GROQ_MODEL"] ?? "llama-3.3-70b-versatile";
        var msgs = new List<object> { new { role = "system", content = AgentPrompt.Build(negocio, agente) } };
        msgs.AddRange(history.TakeLast(12).Select(h => (object)new { role = h.role, content = h.content }));

        var payload = new { model, temperature = 0.6, max_tokens = 220, messages = msgs };
        var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
        var resp = await http.PostAsync("https://api.groq.com/openai/v1/chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode) return new(false, null, "groq: " + await resp.Content.ReadAsStringAsync());

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "…";
        return new(true, reply, null);
    }
}
