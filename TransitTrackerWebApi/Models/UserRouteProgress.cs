using System.ComponentModel.DataAnnotations;

namespace TransitTrackerWebApi.Models;

public class UserRouteProgress
{
    [Key]
    public Guid Id { get; set; }

    public Guid RouteId { get; set; }
    public Guid UserId { get; set; }

    public DateTime CompletedAt { get; set; }

    public Route? Route { get; set; }
    public User? User { get; set; }
}
