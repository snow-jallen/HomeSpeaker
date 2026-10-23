using Microsoft.AspNetCore.Authorization;

namespace HomeSpeaker.Server2.Settings;

/// <summary>Satisfied when no admin PIN is set, or the user unlocked with the admin cookie.</summary>
public sealed class AdminPinRequirement : IAuthorizationRequirement
{
}
