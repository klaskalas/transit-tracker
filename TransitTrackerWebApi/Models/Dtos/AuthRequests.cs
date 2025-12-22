using System.ComponentModel.DataAnnotations;

namespace TransitTrackerWebApi.Models.Dtos;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    string? DisplayName
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    string AccessToken,
    AuthUserResponse User
);

public record AuthUserResponse(
    Guid Id,
    string Email,
    string? DisplayName
);

public record RouteVisitResponse(
    int RouteId,
    bool Completed,
    DateTime? CompletedDate
);

public record UserAchievementDto(
    string AchievementId,
    DateTime UnlockedAt
);

public record UnlockAchievementsRequest(
    List<string> AchievementIds
);
