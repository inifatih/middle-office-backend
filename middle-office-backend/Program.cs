using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Services;
using System.Globalization;

// This host's OS locale (en-ID) uses "," as the decimal separator, which silently corrupted
// every fractional number ClosedXML formatted via culture-sensitive APIs (see MiddleOfficeServices
// GetCellText/GetCellStatusText for the extraction-side fix). Pinning the app to InvariantCulture
// closes off the same class of bug anywhere else .NET does culture-sensitive parsing/formatting
// (e.g. decimal/date model binding).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfigurasi CORS (Disatukan dengan nama spesifik)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMioUI", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Pastikan port ini sesuai dengan frontend Anda
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IMiddleOfficeServices, MiddleOfficeServices>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddOpenApi();

// Auth: belum ada (lihat PRD §6.4 — akan diganti local JWT/LDAP saat modul auth digarap).
// Semua endpoint publik untuk sementara pada tahap standalone development ini.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

app.UseCors("AllowMioUI");

app.MapControllers();

app.Run();