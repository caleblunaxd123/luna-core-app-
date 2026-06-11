using LunaCore.Api.Data;
using LunaCore.Api.Models;

namespace LunaCore.Api.Payments;

public record CheckoutResult(bool Ok, string? Url, string? Message);

/// <summary>
/// Abstracción de pasarela de pago. Implementaciones: MercadoPago (1ª), Stripe, Culqi.
/// Agregar una nueva = implementar esta interfaz y registrarla en DI.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }
    Task<CheckoutResult> CreateCheckoutAsync(Negocio negocio, Plan plan, string baseUrl);
    Task HandleWebhookAsync(string body, AppDbContext db);
}
