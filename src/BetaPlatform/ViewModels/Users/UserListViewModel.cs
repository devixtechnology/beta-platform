namespace BetaPlatform.ViewModels.Users;

/// <summary>One row on <c>/Users</c> (004 — contracts/user-management.md).</summary>
public class UserListViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
