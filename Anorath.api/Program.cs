using System.Text;
using Anorath.api.Services;
using Anorath.Infrastructure.Data;
using Anorath.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

// Every endpoint needs a valid token unless marked [AllowAnonymous]
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing in appsettings.json");

// JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;   // keep claim names as-is: role, companyCode...
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        NameClaimType = "name",
        RoleClaimType = "role"
    };

    // DEBUG: shows in the response Headers why a token was rejected (remove before submission)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var header = ctx.Request.Headers.Authorization.ToString();
            ctx.Response.Headers["X-Debug-Token-Received"] = string.IsNullOrEmpty(header) ? "NO" : "YES";
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            ctx.Response.Headers["X-Debug-Token-Valid"] = "YES";
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            var reason = ctx.Exception.Message.Replace("\r", " ").Replace("\n", " ");
            ctx.Response.Headers["X-Debug-Token-Error"] = reason.Length > 300 ? reason[..300] : reason;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

// Master DB (companies, users, subscriptions)
builder.Services.AddDbContext<MasterErpDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterErp")));

builder.Services.AddScoped<ITenantService, TenantService>();

// Tenant DB: picked per request from the companyCode inside the token
builder.Services.AddScoped<TenantErpDbContext>(provider =>
{
    var tenantService = provider.GetRequiredService<ITenantService>();
    var connectionString = tenantService.GetConnectionStringAsync().GetAwaiter().GetResult();

    var optionsBuilder = new DbContextOptionsBuilder<TenantErpDbContext>();
    optionsBuilder.UseSqlServer(connectionString);

    return new TenantErpDbContext(optionsBuilder.Options);
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Seed Master DB (Super Admin, plans, company admins)
using (var scope = app.Services.CreateScope())
{
    var masterDb = scope.ServiceProvider.GetRequiredService<MasterErpDbContext>();
    await MasterSeeder.SeedAsync(masterDb);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Force HTTPS only when deployed. Locally the client uses http://localhost:5166,
// and a redirect would drop the Authorization header (the token).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();   // 1st: reads the token
app.UseAuthorization();    // 2nd: checks permission
app.MapControllers();      // 3rd: runs the controllers

app.Run();