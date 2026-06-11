using LunaCore.Api.Data;
using LunaCore.Api.Models;

namespace LunaCore.Api.Payments;

/// <summary>Stripe — listo para implementar (mismo contrato). Global, cobra en USD.</summary>
public class StripeGateway : IPaymentGateway
{
    public string Name => "stripe";

    public Task<CheckoutResult> CreateCheckoutAsync(Negocio negocio, Plan plan, string baseUrl)
        => Task.FromResult(new CheckoutResult(false, null, "Stripe aún no está configurado."));

    public Task HandleWebhookAsync(string body, AppDbContext db) => Task.CompletedTask;
}
