using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ElectricityTimeSeries.Api.Authorization.Enforcement;

/// <summary>Asks the enforcement point about every token that passed the signature, issuer, audience and
/// lifetime checks. A denied token is treated like an invalid one: 401.</summary>
public sealed class PolicyEnforcingJwtBearerEvents : JwtBearerEvents
{
    private readonly IPolicyEnforcementPoint _enforcementPoint;
    private readonly ILogger<PolicyEnforcingJwtBearerEvents> _logger;

    public PolicyEnforcingJwtBearerEvents(IPolicyEnforcementPoint enforcementPoint, ILogger<PolicyEnforcingJwtBearerEvents> logger)
    {
        _enforcementPoint = enforcementPoint;
        _logger = logger;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.SecurityToken is not JsonWebToken token)
        {
            context.Fail("Unsupported access token.");
            return;
        }

        var attributes = new AccessTokenAttributes(
            Subject: Claim(token, "sub"),
            ClientId: Claim(token, "client_id") ?? Claim(token, "azp"),
            TenantId: Claim(token, "tid"),
            IssuedAt: token.IssuedAt == DateTime.MinValue ? null : new DateTimeOffset(token.IssuedAt, TimeSpan.Zero));

        var decision = await _enforcementPoint.EvaluateAsync(attributes, context.HttpContext.RequestAborted);
        if (!decision.IsPermitted)
        {
            _logger.LogWarning(
                "Refused a revoked access token (client {ClientId}, tenant {TenantId}): {Reason}",
                attributes.ClientId, attributes.TenantId, decision.Reason);
            context.Fail(decision.Reason!);
        }
    }

    private static string? Claim(JsonWebToken token, string type) =>
        token.TryGetPayloadValue<string>(type, out var value) ? value : null;
}
