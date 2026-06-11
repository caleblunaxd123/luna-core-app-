using System.Text;
using System.Text.Json;
using LunaCore.Api.Data;
using LunaCore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LunaCore.Api.Payments;

/// <summary>
/// MercadoPago — Checkout Pro (acepta Yape, tarjetas y otros medios en Perú).
/// Requiere MercadoPago:AccessToken en config/user-secrets/env (lo pone el dueño).
/// Nota: para cobro recurrente real se usa "preapproval"; aquí va checkout por preferencia
/// como base funcional — se eleva a suscripción al integrar con credenciales reales.
/// </summary>
public class MercadoPagoGateway(IHttpClientFactory httpFactory, IConfiguration cfg) : IPaymentGateway
{
    public string Name => "mercadopago";

    public async Task<CheckoutResult> CreateCheckoutAsync(Negocio negocio, Plan plan, string baseUrl)
    {
        var token = cfg["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(token))
            return new(false, null, "Para activar pagos, configura MercadoPago:AccessToken en el servidor.");

        var pref = new
        {
            items = new[] { new {
                title = $"Luna Core — Plan {plan.Nombre}",
                quantity = 1,
                unit_price = (double)plan.PrecioMensual,
                currency_id = "PEN"
            }},
            external_reference = $"{negocio.Id}:{plan.Id}",
            payer = new { email = negocio.Email },
            back_urls = new {
                success = $"{baseUrl}/panel/?pago=ok",
                failure = $"{baseUrl}/panel/?pago=error",
                pending = $"{baseUrl}/panel/?pago=pendiente"
            },
            auto_return = "approved",
            notification_url = $"{baseUrl}/api/billing/webhook/mercadopago"
        };

        var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var resp = await http.PostAsync("https://api.mercadopago.com/checkout/preferences",
            new StringContent(JsonSerializer.Serialize(pref), Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode)
            return new(false, null, "MercadoPago rechazó la solicitud: " + await resp.Content.ReadAsStringAsync());

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var url = doc.RootElement.GetProperty("init_point").GetString();
        return new(true, url, null);
    }

    public async Task HandleWebhookAsync(string body, AppDbContext db)
    {
        db.PagoEventos.Add(new PagoEvento { Gateway = Name, Tipo = "notification", Payload = body });
        await db.SaveChangesAsync();

        var token = cfg["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            string? paymentId = null;
            using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body))
                if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var pid))
                    paymentId = pid.ValueKind == JsonValueKind.String ? pid.GetString() : pid.GetRawText();
            if (string.IsNullOrWhiteSpace(paymentId)) return;

            var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            var resp = await http.GetAsync($"https://api.mercadopago.com/v1/payments/{paymentId}");
            if (!resp.IsSuccessStatusCode) return;

            using var pago = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var status = pago.RootElement.GetProperty("status").GetString();
            var extRef = pago.RootElement.TryGetProperty("external_reference", out var er) ? er.GetString() : null;
            if (status != "approved" || string.IsNullOrWhiteSpace(extRef) || !extRef.Contains(':')) return;

            var parts = extRef.Split(':');
            if (!int.TryParse(parts[0], out var negocioId) || !int.TryParse(parts[1], out var planId)) return;

            var negocio = await db.Negocios.FindAsync(negocioId);
            if (negocio is null) return;
            negocio.PlanId = planId;   // activa el plan pagado
            db.Suscripciones.Add(new Suscripcion
            {
                NegocioId = negocioId, PlanId = planId, Estado = "active",
                Gateway = Name, GatewayRef = paymentId, ProximaRenovacion = DateTime.UtcNow.AddMonths(1)
            });
            await db.SaveChangesAsync();
        }
        catch { /* no romper el webhook ante payloads inesperados */ }
    }
}
