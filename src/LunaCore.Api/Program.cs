using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LunaCore.Api.Data;
using LunaCore.Api.Models;
using LunaCore.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPasswordHasher<Negocio>, PasswordHasher<Negocio>>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "LunaCore.Api" }));

/* ── Auth ── */
app.MapPost("/api/auth/register", async (RegisterReq r, AppDbContext db, IPasswordHasher<Negocio> hasher, JwtService jwt) =>
{
    if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
        return Results.BadRequest(new { error = "Email y contraseña son obligatorios." });
    var email = r.Email.Trim().ToLowerInvariant();
    if (await db.Negocios.AnyAsync(n => n.Email == email))
        return Results.Conflict(new { error = "Ese email ya está registrado." });

    var n = new Negocio
    {
        Nombre = r.Nombre?.Trim() ?? "",
        Email = email,
        Rubro = string.IsNullOrWhiteSpace(r.Rubro) ? "estetica" : r.Rubro!.Trim(),
        PlanId = 1
    };
    n.PasswordHash = hasher.HashPassword(n, r.Password);
    db.Negocios.Add(n);
    await db.SaveChangesAsync();
    return Results.Ok(new { token = jwt.Create(n), negocio = new { n.Id, n.Nombre, n.Email, n.Rubro, plan = "Free" } });
});

app.MapPost("/api/auth/login", async (LoginReq r, AppDbContext db, IPasswordHasher<Negocio> hasher, JwtService jwt) =>
{
    var email = (r.Email ?? "").Trim().ToLowerInvariant();
    var n = await db.Negocios.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Email == email);
    if (n is null) return Results.Json(new { error = "Credenciales inválidas." }, statusCode: 401);
    if (hasher.VerifyHashedPassword(n, n.PasswordHash, r.Password ?? "") == PasswordVerificationResult.Failed)
        return Results.Json(new { error = "Credenciales inválidas." }, statusCode: 401);
    return Results.Ok(new { token = jwt.Create(n), negocio = new { n.Id, n.Nombre, n.Email, n.Rubro, plan = n.Plan?.Nombre } });
});

/* ── Perfil + uso ── */
app.MapGet("/api/me", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var id = NegocioId(user);
    var n = await db.Negocios.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == id);
    if (n is null) return Results.Unauthorized();
    var periodo = DateTime.UtcNow.ToString("yyyyMM");
    var uso = await db.UsosMensuales.FirstOrDefaultAsync(u => u.NegocioId == id && u.Periodo == periodo);
    return Results.Ok(new
    {
        n.Id, n.Nombre, n.Email, n.Rubro,
        plan = n.Plan?.Nombre,
        limite = n.Plan?.LimiteMensajes ?? 0,
        usados = uso?.Mensajes ?? 0
    });
}).RequireAuthorization();

/* ── Chat: el Agente de Ventas, con límite POR CUENTA ── */
app.MapPost("/api/chat", async (ChatRequest req, ClaimsPrincipal user, AppDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg) =>
{
    var id = NegocioId(user);
    var negocio = await db.Negocios.Include(n => n.Plan).FirstOrDefaultAsync(n => n.Id == id);
    if (negocio is null) return Results.Unauthorized();

    var periodo = DateTime.UtcNow.ToString("yyyyMM");
    var uso = await db.UsosMensuales.FirstOrDefaultAsync(u => u.NegocioId == id && u.Periodo == periodo);
    if (uso is null) { uso = new UsoMensual { NegocioId = id, Periodo = periodo, Mensajes = 0 }; db.UsosMensuales.Add(uso); }

    var limite = negocio.Plan?.LimiteMensajes ?? 50;
    if (uso.Mensajes >= limite)
        return Results.Json(new { error = "limit", limit = true, plan = negocio.Plan?.Nombre, limite }, statusCode: 429);

    var key = cfg["GROQ_API_KEY"];
    if (string.IsNullOrWhiteSpace(key))
        return Results.Json(new { error = "config", message = "Falta GROQ_API_KEY (env o user-secrets)." }, statusCode: 500);

    var rubro = Rubros.Get(negocio.Rubro);
    var model = cfg["GROQ_MODEL"] ?? "llama-3.3-70b-versatile";
    var messages = req.Messages ?? new();

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
    var resp = await http.PostAsync("https://api.groq.com/openai/v1/chat/completions",
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    if (!resp.IsSuccessStatusCode)
        return Results.Json(new { error = "groq", detail = await resp.Content.ReadAsStringAsync() }, statusCode: 502);

    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "…";

    uso.Mensajes++;
    await db.SaveChangesAsync();
    return Results.Ok(new { reply, model, usados = uso.Mensajes, limite, remaining = Math.Max(0, limite - uso.Mensajes) });
}).RequireAuthorization();

// Aplica migraciones pendientes al iniciar (dev)
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.Run();

static int NegocioId(ClaimsPrincipal user) =>
    int.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : 0;

/* ── DTOs ── */
record RegisterReq(
    [property: JsonPropertyName("nombre")] string? Nombre,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("password")] string? Password,
    [property: JsonPropertyName("rubro")] string? Rubro);

record LoginReq(
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("password")] string? Password);

record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

record ChatRequest(
    [property: JsonPropertyName("messages")] List<ChatMessage>? Messages);

/* ── Config por rubro (se moverá a tabla AgenteConfig en el panel) ── */
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
