using System.ComponentModel.DataAnnotations;

namespace TransitTrackerWebApi.Models;

public class UserAuthProvider
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ProviderUserId { get; set; } = string.Empty;

    public User? User { get; set; }
}
