using Hermes.API.Cookies;
using Hermes.Application.Abstraction;
using Hermes.Application.Entities;
using Hermes.Application.Services;
using Hermes.Infrastructure.Config;
using Hermes.Infrastructure.Context.Extension;
using Hermes.Infrastructure.CronJobs.Extension;
using Hermes.Infrastructure.Email;
using Hermes.Infrastructure.Factory;
using Hermes.Infrastructure.BanListFileHandler;
using Hermes.Infrastructure.BanListConverter;
using Hermes.Application.Mapper;
using Hermes.Infrastructure.Repositories;
using Hermes.Infrastructure.Seed;
using Hermes.Infrastructure.TokenGenerator;
using Microsoft.AspNetCore.Identity;
using MimeKit;
using Hermes.Infrastructure.Helpers;
using Hermes.Application.Services.Extension;
using Elastic.Clients.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);
DotNetEnv.Env.Load("../.env");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddHttpClient();
builder.Services.AddFluentValidation();

builder.Services.AddSingleton<AppContextFactory>();
builder.Services.AddSingleton<EmailConfig>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenGenerator, TokenGenerator>();
builder.Services.AddSingleton<BanListConverter>();
builder.Services.AddSingleton<CookieManager>();
builder.Services.AddSingleton<BanListFileHandler>();
builder.Services.AddSingleton<ElasticsearchClient>(provider =>
{
    var url = Environment.GetEnvironmentVariable("ELASTICSEARCH_URL");
    var settings = new ElasticsearchClientSettings(new Uri(url!))
    .DefaultIndex("npm-logs");

    return new ElasticsearchClient(settings);
});

builder.Services.AddScoped<MimeMessage>();
builder.Services.AddScoped<IServerDataService, ServerDataService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IElasticService, ElasticService>();
builder.Services.AddScoped<HttpClientSender>();

builder.Services.AddScoped<IElasticRepository>(provider =>
{
    var elasticClient = provider.GetRequiredService<ElasticsearchClient>();

    return new ElasticRepository(elasticClient);
});

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

builder.Services.AddScoped<IServerDataRepository>(provider =>
{
    var factory = provider.GetRequiredService<AppContextFactory>();
    var context = factory.CreateDbContext(args);

    return new ServerDataRepository(context);
});

builder.Services.AddAuthentication("auth-scheme")
    .AddCookie("auth-scheme", options =>
    {
        options.Cookie.Name = "auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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

builder.Services.AddCronJobs();

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("cors-policy");
app.UseAuthentication();
app.UseAuthorization();

if (EnvironmentHelper.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.ApplyMigrations(args);

await app.Services.SeedAdminUser();
await app.Services.SeedToken();

app.Run();