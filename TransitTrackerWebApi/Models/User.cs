using System.ComponentModel.DataAnnotations;

namespace TransitTrackerWebApi.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? DisplayName { get; set; }

    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserAuthProvider> AuthProviders { get; set; } = new List<UserAuthProvider>();
}
