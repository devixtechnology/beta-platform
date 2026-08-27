using Microsoft.AspNetCore.Identity;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Application user (single administrative role in Phase 1 — FR-002).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();
    public bool IsActive { get; set; } = true;
}
