using LunaCore.Api.Data;
using LunaCore.Api.Models;

namespace LunaCore.Api.Payments;

/// <summary>Culqi — pasarela peruana, listo para implementar (mismo contrato).</summary>
public class CulqiGateway : IPaymentGateway
{
    public string Name => "culqi";

    public Task<CheckoutResult> CreateCheckoutAsync(Negocio negocio, Plan plan, string baseUrl)
        => Task.FromResult(new CheckoutResult(false, null, "Culqi aún no está configurado."));

    public Task HandleWebhookAsync(string body, AppDbContext db) => Task.CompletedTask;
}
