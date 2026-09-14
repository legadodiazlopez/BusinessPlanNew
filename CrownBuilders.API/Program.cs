using Microsoft.EntityFrameworkCore;
using CrownBuilders.API.Data;
using CrownBuilders.API.Hubs;
using CrownBuilders.API.Services;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURACIÓN DE BASE DE DATOS ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=CrownBuildersDB;Trusted_Connection=True;";

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

// --- AGREGAMOS SERVICIO DE NOTIFICACIONES FIREBASE ---
builder.Services.AddSingleton<FirebaseNotificationService>();

var app = builder.Build();

// --- PIPELINE DE PETICIONES ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --- ACTIVAMOS CORS ---
app.UseCors("PermitirTodo");

app.UseAuthorization();
app.MapControllers();

// --- MAPEAMOS EL HUB ---
app.MapHub<ChatHub>("/api/chathub");

app.Run();