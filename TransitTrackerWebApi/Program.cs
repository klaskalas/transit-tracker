using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TransitTrackerWebApi.Repositories;
using TransitTrackerWebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseNetTopologySuite())
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<RoutesRepository>();
builder.Services.AddScoped<IRoutesService, RoutesService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"] ?? string.Empty))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policyBuilder =>
        {
            policyBuilder
                .WithOrigins("http://localhost:4200")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// // Get all routes
// app.MapGet("/api/routes", async (AppDbContext db) =>
//     await db.Routes
//         .Select(r => new { r.Id, r.ShortName })
//         .ToListAsync()
// );
//
// // Get GeoJSON shape for a route
// app.MapGet("/api/routes/{id:guid}/shape", async (Guid id, AppDbContext db) =>
// {
//     var route = await db.Routes.FindAsync(id);
//     return route is null ? Results.NotFound() : Results.Ok(route.GeoJsonShape);
// });
//
// // Mark a route as completed for a user (simple auth with userId in query)
// app.MapPost("/api/routes/{id:guid}/complete", async (Guid id, Guid userId, AppDbContext db) =>
// {
//     var exists = await db.UserRouteProgress
//         .AnyAsync(p => p.RouteId == id && p.UserId == userId);
//
//     if (!exists)
//     {
//         db.UserRouteProgress.Add(new UserRouteProgress
//         {
//             Id = Guid.NewGuid(),
//             RouteId = id,
//             UserId = userId,
//             CompletedAt = DateTime.UtcNow
//         });
//         await db.SaveChangesAsync();
//     }
//
//     return Results.Ok();
// });

app.Run();
