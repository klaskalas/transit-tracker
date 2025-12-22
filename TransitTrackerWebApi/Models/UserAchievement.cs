using System.ComponentModel.DataAnnotations;

namespace TransitTrackerWebApi.Models;

public class UserAchievement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string AchievementId { get; set; } = string.Empty;

    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    public int? ProgressAtUnlock { get; set; }

    public User? User { get; set; }
}
