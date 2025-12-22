using Microsoft.EntityFrameworkCore;
using TransitTrackerWebApi.Models;
using Route = TransitTrackerWebApi.Models.Route;

namespace TransitTrackerWebApi.Repositories;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<ShapeLine> ShapeLines => Set<ShapeLine>();
    public DbSet<Shape> Shapes => Set<Shape>();
    public DbSet<RouteShape> RouteShapes => Set<RouteShape>();
    public DbSet<UserRouteProgress> UserRouteProgress => Set<UserRouteProgress>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserAuthProvider> UserAuthProviders => Set<UserAuthProvider>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteShape>()
            .HasKey(rs => new { rs.RouteId, rs.GtfsShapeId });

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasIndex(provider => new { provider.Provider, provider.ProviderUserId })
            .IsUnique();

        modelBuilder.Entity<UserAuthProvider>()
            .HasIndex(provider => new { provider.UserId, provider.Provider })
            .IsUnique();

        modelBuilder.Entity<UserAchievement>()
            .HasIndex(achievement => new { achievement.UserId, achievement.AchievementId })
            .IsUnique();

        modelBuilder.Entity<UserRouteProgress>()
            .HasOne(progress => progress.Route)
            .WithMany()
            .HasForeignKey(progress => progress.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRouteProgress>()
            .HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAchievement>()
            .HasOne(achievement => achievement.User)
            .WithMany()
            .HasForeignKey(achievement => achievement.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
