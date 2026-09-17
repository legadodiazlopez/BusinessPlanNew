using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CrownBuilders.API.Data;
using CrownBuilders.API.Hubs;
using CrownBuilders.API.Services;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

// --- CONFIGURACIÓN DE BASE DE DATOS ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "La configuración ConnectionStrings:DefaultConnection es obligatoria.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// --- AGREGAMOS CORS (CORREGIDO PARA SIGNALR) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- AGREGAMOS SIGNALR Y CONTROLADORES ---
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException(
        "La configuración Jwt:Key es obligatoria y debe tener al menos 32 caracteres.");
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var tokenService = new JwtTokenService(jwtOptions);
        options.TokenValidationParameters = tokenService.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                var requestPath = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    requestPath.StartsWithSegments("/api/chathub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<MessagePersistenceService>();
builder.Services.AddScoped<MessageDeliveryService>();
builder.Services.AddSingleton<ActiveConnectionRegistry>();

// --- AGREGAMOS SERVICIO DE NOTIFICACIONES FIREBASE ---
builder.Services.AddSingleton<FirebaseNotificationService>();

var app = builder.Build();

// Crear o actualizar la base local al iniciar la API en desarrollo.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudo preparar LocalDB. La API se iniciará, pero los endpoints que usan base de datos no estarán disponibles.");
    }
}

// --- PIPELINE DE PETICIONES ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// --- ACTIVAMOS CORS ---
app.UseCors("PermitirTodo");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// --- MAPEAMOS EL HUB ---
app.MapHub<ChatHub>("/api/chathub");

app.Run();
