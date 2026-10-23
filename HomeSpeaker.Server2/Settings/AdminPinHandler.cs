using Microsoft.AspNetCore.Authorization;

namespace HomeSpeaker.Server2.Settings;

public sealed class AdminPinHandler(IConfiguration configuration) : AuthorizationHandler<AdminPinRequirement>
{
    public const string SchemeName = "HomeSpeakerAdmin";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminPinRequirement requirement)
    {
        var unlocked = context.User.Identities.Any(i =>
            i.IsAuthenticated && string.Equals(i.AuthenticationType, SchemeName, StringComparison.Ordinal));
        if (!AdminPin.IsSet(configuration) || unlocked)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
