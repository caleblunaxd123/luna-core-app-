using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "LunaCore.Api" }));

/* ── Chat: el "Agente de Ventas". Server-side, con límite (en Sprint 2 será por cuenta). ── */
const int LIMIT = 6;

app.MapPost("/api/chat", async (ChatRequest req, IHttpClientFactory httpFactory, IConfiguration cfg) =>
{
    var key = cfg["GROQ_API_KEY"];
    if (string.IsNullOrWhiteSpace(key))
        return Results.Json(new { error = "config", message = "Falta GROQ_API_KEY (env o user-secrets)." }, statusCode: 500);

    var messages = req.Messages ?? new();
    var userTurns = messages.Count(m => m.Role == "user");
    if (userTurns > LIMIT)
        return Results.Json(new { error = "limit", limit = true }, statusCode: 429);

    var rubro = Rubros.Get(req.Rubro);
    var model = cfg["GROQ_MODEL"] ?? "llama-3.3-70b-versatile";

    var payload = new
    {
        model,
        temperature = 0.6,
        max_tokens = 220,
        messages = new[] { new { role = "system", content = Rubros.BuildSystem(rubro) } }
            .Concat(messages.TakeLast(12).Select(m => new { role = m.Role, content = m.Content }))
            .ToArray()
    };

    var http = httpFactory.CreateClient();
    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
    var resp = await http.PostAsync(
        "https://api.groq.com/openai/v1/chat/completions",
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

    if (!resp.IsSuccessStatusCode)
        return Results.Json(new { error = "groq", detail = await resp.Content.ReadAsStringAsync() }, statusCode: 502);

    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "…";
    return Results.Ok(new { reply, model, remaining = Math.Max(0, LIMIT - userTurns) });
});

app.Run();

/* ── Modelos ── */
record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

record ChatRequest(
    [property: JsonPropertyName("rubro")] string? Rubro,
    [property: JsonPropertyName("messages")] List<ChatMessage>? Messages);

/* ── Config por rubro (se moverá a BD/AgenteConfig en Sprint 2) ── */
record Rubro(string Agente, string Negocio, string Cliente, string Evento, string Servicio, string Ticket, int Adelanto);

static class Rubros
{
    static readonly Dictionary<string, Rubro> Map = new()
    {
        ["estetica"]     = new("Bella", "clínica de estética", "paciente", "cita", "tratamientos faciales y corporales (limpieza facial, botox, láser)", "S/ 150 a S/ 800", 50),
        ["dental"]       = new("Sonríe", "clínica dental", "paciente", "cita", "limpieza, ortodoncia, implantes", "S/ 80 a S/ 1500", 40),
        ["inmobiliaria"] = new("Mateo", "inmobiliaria", "interesado", "visita", "venta y alquiler de departamentos en Lima", "US$ 90k a US$ 300k", 0),
        ["gimnasio"]     = new("Fit", "gimnasio", "interesado", "clase de prueba", "membresías, funcional, pesas y clases grupales", "S/ 120 a S/ 200 al mes", 0),
        ["restaurante"]  = new("Sazón", "restaurante", "cliente", "reserva", "reservas de mesa y eventos", "S/ 80 a S/ 300 por persona", 0),
        ["veterinaria"]  = new("Huellitas", "veterinaria", "cliente", "cita", "consultas, vacunas, baño y peluquería", "S/ 60 a S/ 250", 30),
    };

    public static Rubro Get(string? key) => key != null && Map.TryGetValue(key, out var r) ? r : Map["estetica"];

    public static string BuildSystem(Rubro c)
    {
        var pago = c.Adelanto > 0
            ? $"Para asegurar la asistencia y reducir las inasistencias, pide un adelanto de S/{c.Adelanto} por Yape para confirmar la {c.Evento} (menciona que se descuenta del total). Si el {c.Cliente} duda, explica con calidez que así le garantizas el cupo."
            : $"Para confirmar la {c.Evento}, pide solo el nombre completo del {c.Cliente}.";
        return string.Join("\n", new[]
        {
            $"Eres {c.Agente}, asistente de ventas por WhatsApp de un(a) {c.Negocio} en Lima, Perú.",
            $"Ofreces: {c.Servicio}. Rango de precios referencial: {c.Ticket}.",
            $"Tu objetivo es: responder al instante y con calidez, calificar al {c.Cliente} con 1 o 2 preguntas, ofrecer 2 horarios concretos y agendar la {c.Evento}. {pago}",
            "REGLAS DE ESTILO: escribe como en WhatsApp, mensajes MUY cortos (1 a 3 frases), tono cercano y peruano, máximo un emoji por mensaje, UNA sola pregunta por turno. No inventes datos que no tengas (di que lo confirmas). No hables de temas ajenos al negocio; si ocurre, redirige amablemente a agendar.",
            $"Es de noche y atiendes 24/7: si es oportuno, recuérdalo con naturalidad. Cuando el {c.Cliente} acepte (y, si aplica, confirme el adelanto), confirma la {c.Evento} con día, hora y un recordatorio.",
        });
    }
}
