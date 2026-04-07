namespace ILSpy.Mcp.TestTargets;

public class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

public class AdminUser : User
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
