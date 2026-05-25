// =====================================================================
// DIGITAL TWIN -- Dashboard (Blazor + Babylon.js 3D Visualization)
// =====================================================================
// Consumes telemetry and events from Surgewave topics and renders a
// real-time 3D factory floor with equipment status, anomaly detection,
// and time-travel replay.
// =====================================================================

using Kuestenlogik.Surgewave.Samples.DigitalTwin.Dashboard.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// EquipmentDataService is a BackgroundService that consumes from Surgewave topics
// (digitaltwin-telemetry, digitaltwin-events) and maintains in-memory state
// for the dashboard. Registered as both singleton and hosted service so
// Blazor components can inject it while it runs continuously.
builder.Services.AddSingleton<EquipmentDataService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EquipmentDataService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Kuestenlogik.Surgewave.Samples.DigitalTwin.Dashboard.Components.App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("=== Digital Twin Dashboard ===");
Console.WriteLine("Open http://localhost:5000 in your browser");

app.Run();
