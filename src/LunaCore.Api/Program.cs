using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LunaCore.Api.Data;
using LunaCore.Api.Models;
using LunaCore.Api.Payments;
using LunaCore.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IPasswordHasher<Negocio>, PasswordHasher<Negocio>>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<AgentRunner>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<IPaymentGateway, MercadoPagoGateway>();
builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
builder.Services.AddScoped<IPaymentGateway, CulqiGateway>();
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
    db.AgentesConfig.Add(new AgenteConfig { NegocioId = n.Id, Rol = "ventas", NombreAgente = AgentPrompt.DefaultNombre(n.Rubro) });
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

/* ── Configuración del empleado digital ── */
app.MapGet("/api/agente", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var id = NegocioId(user);
    var c = await db.AgentesConfig.FirstOrDefaultAsync(a => a.NegocioId == id);
    if (c is null) return Results.NotFound();
    return Results.Ok(new { c.Rol, c.NombreAgente, c.BaseConocimiento, c.Horarios, c.Adelanto, c.Activo });
}).RequireAuthorization();

app.MapPut("/api/agente", async (AgenteReq r, ClaimsPrincipal user, AppDbContext db) =>
{
    var id = NegocioId(user);
    var c = await db.AgentesConfig.FirstOrDefaultAsync(a => a.NegocioId == id);
    if (c is null) { c = new AgenteConfig { NegocioId = id }; db.AgentesConfig.Add(c); }
    if (r.Rol is not null) c.Rol = r.Rol;
    if (r.NombreAgente is not null) c.NombreAgente = r.NombreAgente;
    if (r.BaseConocimiento is not null) c.BaseConocimiento = r.BaseConocimiento;
    if (r.Horarios is not null) c.Horarios = r.Horarios;
    if (r.Adelanto.HasValue) c.Adelanto = r.Adelanto.Value;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

/* ── Chat: el empleado digital, con límite POR CUENTA y config del negocio ── */
app.MapPost("/api/chat", async (ChatRequest req, ClaimsPrincipal user, AppDbContext db, AgentRunner runner) =>
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

    var agente = await db.AgentesConfig.FirstOrDefaultAsync(a => a.NegocioId == id) ?? new AgenteConfig { Rol = "ventas" };
    var history = (req.Messages ?? new()).Select(m => (m.Role, m.Content)).ToList();
    var result = await runner.ReplyAsync(negocio, agente, history);
    if (!result.Ok)
        return result.Error == "config"
            ? Results.Json(new { error = "config", message = "Falta GROQ_API_KEY (env o user-secrets)." }, statusCode: 500)
            : Results.Json(new { error = "groq", detail = result.Error }, statusCode: 502);

    uso.Mensajes++;
    await db.SaveChangesAsync();
    return Results.Ok(new { reply = result.Reply, usados = uso.Mensajes, limite, remaining = Math.Max(0, limite - uso.Mensajes) });
}).RequireAuthorization();

/* ── Leads capturados (por WhatsApp) ── */
app.MapGet("/api/leads", async (ClaimsPrincipal user, AppDbContext db) =>
{
    var id = NegocioId(user);
    var leads = await db.Leads.Where(l => l.NegocioId == id).OrderByDescending(l => l.CreatedAt).Take(100)
        .Select(l => new { l.Contacto, l.Mensaje, l.Canal, l.CreatedAt }).ToListAsync();
    return Results.Ok(leads);
}).RequireAuthorization();

/* ── WhatsApp: conectar el número del negocio (guarda phone_number_id) ── */
app.MapPut("/api/whatsapp/connect", async (WaConnectReq r, ClaimsPrincipal user, AppDbContext db) =>
{
    var id = NegocioId(user);
    var n = await db.Negocios.FindAsync(id);
    if (n is null) return Results.Unauthorized();
    n.WhatsappPhoneNumberId = r.PhoneNumberId;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

/* ── WhatsApp Cloud API: verificación del webhook (Meta) ── */
app.MapGet("/api/whatsapp/webhook", (HttpRequest req, IConfiguration cfg) =>
{
    var mode = req.Query["hub.mode"].ToString();
    var verify = req.Query["hub.verify_token"].ToString();
    var challenge = req.Query["hub.challenge"].ToString();
    return (mode == "subscribe" && verify == cfg["WhatsApp:VerifyToken"])
        ? Results.Text(challenge) : Results.Unauthorized();
});

/* ── WhatsApp Cloud API: recibe mensajes → agente responde + captura lead ── */
app.MapPost("/api/whatsapp/webhook", async (HttpRequest httpReq, AppDbContext db, AgentRunner runner, WhatsAppService wa) =>
{
    using var reader = new StreamReader(httpReq.Body);
    var body = await reader.ReadToEndAsync();
    try
    {
        using var doc = JsonDocument.Parse(body);
        var value = doc.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");
        if (!value.TryGetProperty("messages", out var msgs)) return Results.Ok(); // estados de entrega, etc.

        var phoneNumberId = value.GetProperty("metadata").GetProperty("phone_number_id").GetString();
        var msg = msgs[0];
        var from = msg.GetProperty("from").GetString() ?? "";
        var text = msg.TryGetProperty("text", out var t) ? (t.GetProperty("body").GetString() ?? "") : "";

        var negocio = await db.Negocios.Include(n => n.Plan).FirstOrDefaultAsync(n => n.WhatsappPhoneNumberId == phoneNumberId);
        if (negocio is null) return Results.Ok();

        var periodo = DateTime.UtcNow.ToString("yyyyMM");
        var uso = await db.UsosMensuales.FirstOrDefaultAsync(u => u.NegocioId == negocio.Id && u.Periodo == periodo);
        if (uso is null) { uso = new UsoMensual { NegocioId = negocio.Id, Periodo = periodo }; db.UsosMensuales.Add(uso); }
        var limite = negocio.Plan?.LimiteMensajes ?? 50;

        db.Leads.Add(new Lead { NegocioId = negocio.Id, Contacto = from, Mensaje = text });

        if (uso.Mensajes < limite)
        {
            var agente = await db.AgentesConfig.FirstOrDefaultAsync(a => a.NegocioId == negocio.Id) ?? new AgenteConfig { Rol = "ventas" };
            var result = await runner.ReplyAsync(negocio, agente, new() { ("user", text) });
            if (result.Ok) { await wa.SendAsync(from, result.Reply!); uso.Mensajes++; }
        }
        await db.SaveChangesAsync();
    }
    catch { /* no romper el webhook ante payloads inesperados */ }
    return Results.Ok();
});

/* ── Planes y pagos ── */
app.MapGet("/api/plans", async (AppDbContext db) =>
    Results.Ok(await db.Planes.OrderBy(p => p.PrecioMensual)
        .Select(p => new { p.Id, p.Nombre, p.PrecioMensual, p.LimiteMensajes }).ToListAsync()));

app.MapPost("/api/billing/checkout", async (CheckoutReq req, ClaimsPrincipal user, AppDbContext db, IEnumerable<IPaymentGateway> gateways, HttpContext ctx) =>
{
    var id = NegocioId(user);
    var negocio = await db.Negocios.FindAsync(id);
    if (negocio is null) return Results.Unauthorized();
    var plan = await db.Planes.FindAsync(req.PlanId);
    if (plan is null) return Results.BadRequest(new { error = "Plan inválido." });

    var gw = gateways.FirstOrDefault(g => g.Name == (req.Gateway ?? "mercadopago"));
    if (gw is null) return Results.BadRequest(new { error = "Pasarela no soportada." });

    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var result = await gw.CreateCheckoutAsync(negocio, plan, baseUrl);
    return result.Ok
        ? Results.Ok(new { url = result.Url })
        : Results.Json(new { error = "checkout", message = result.Message }, statusCode: 400);
}).RequireAuthorization();

app.MapPost("/api/billing/webhook/{gateway}", async (string gateway, HttpRequest httpReq, AppDbContext db, IEnumerable<IPaymentGateway> gateways) =>
{
    using var reader = new StreamReader(httpReq.Body);
    var body = await reader.ReadToEndAsync();
    var gw = gateways.FirstOrDefault(g => g.Name == gateway);
    if (gw is not null) await gw.HandleWebhookAsync(body, db);
    return Results.Ok();
});

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

record CheckoutReq(
    [property: JsonPropertyName("planId")] int PlanId,
    [property: JsonPropertyName("gateway")] string? Gateway);

record AgenteReq(
    [property: JsonPropertyName("rol")] string? Rol,
    [property: JsonPropertyName("nombreAgente")] string? NombreAgente,
    [property: JsonPropertyName("baseConocimiento")] string? BaseConocimiento,
    [property: JsonPropertyName("horarios")] string? Horarios,
    [property: JsonPropertyName("adelanto")] int? Adelanto);

record WaConnectReq(
    [property: JsonPropertyName("phoneNumberId")] string? PhoneNumberId);

record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

record ChatRequest(
    [property: JsonPropertyName("messages")] List<ChatMessage>? Messages);
