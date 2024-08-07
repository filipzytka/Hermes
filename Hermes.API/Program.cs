using Hermes.Application.Abstraction;
using Hermes.Application.Entities;
using Hermes.Application.Services;
using Microsoft.AspNetCore.Identity;
using MimeKit;

var builder = WebApplication.CreateBuilder(args);
var envVar = DotNetEnv.Env.Load("../Hermes.Infrastructure/.env");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddSingleton<AppContextFactory>();
builder.Services.AddSingleton<MimeMessage>();
builder.Services.AddSingleton<IEmailConfig, EmailConfig>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenGenerator, TokenGenerator>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailRepository>(provider =>
{
    var message = provider.GetRequiredService<MimeMessage>();
    var config = provider.GetRequiredService<IEmailConfig>();

    return new EmailRepository(message, config);
builder.Services.AddScoped<ITokenRepository>(provider =>
{
    var factory = provider.GetRequiredService<AppContextFactory>();
    var context = factory.CreateDbContext(args);
    var tokenGenerator = provider.GetRequiredService<ITokenGenerator>();

    return new TokenRepository(context, tokenGenerator);
});

builder.Services.AddScoped<IUserRepository>(provider =>
{
    var factory = provider.GetRequiredService<AppContextFactory>();
    var context = factory.CreateDbContext(args);

    var hasher = provider.GetRequiredService<IPasswordHasher<User>>();

    return new UserRepository(context, hasher);
});

builder.Services.AddAuthentication("auth-scheme")
    .AddCookie("auth-scheme", options =>
    {
        options.Cookie.Name = "auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddCookie("active-scheme", options =>
    {
        options.Cookie.Name = "active";
        options.Cookie.HttpOnly = false;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("cors-policy", (pb) =>
    {
        pb.WithOrigins(Environment.GetEnvironmentVariable("REACT_CLIENT_URL")!)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("cors-policy");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("cors-policy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.Services.SeedAdminUser();

app.Run();