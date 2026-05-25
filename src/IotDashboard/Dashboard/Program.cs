// =====================================================================
// IOT DASHBOARD -- Blazor Real-Time Sensor Monitoring
// =====================================================================
// Consumes sensor readings from Surgewave topic "iot-sensors" and displays
// live charts, threshold alerts, and running statistics using MudBlazor.
// =====================================================================

using Kuestenlogik.Surgewave.Samples.IotDashboard.Dashboard.Components;
using Kuestenlogik.Surgewave.Samples.IotDashboard.Dashboard.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// SensorDataService is a BackgroundService that consumes from the "iot-sensors"
// Surgewave topic and maintains sensor state for real-time dashboard display.
// Registered as both singleton (for Blazor component injection) and hosted
// service (for continuous background consumption).
builder.Services.AddSingleton<SensorDataService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SensorDataService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
