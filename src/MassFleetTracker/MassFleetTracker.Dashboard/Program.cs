using MassFleetTracker.Dashboard.Components;
using MassFleetTracker.Dashboard.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Core services (singletons shared across all clients)
builder.Services.AddSingleton<AggregationService>();
builder.Services.AddSingleton<TimeSeriesBuffer>();
builder.Services.AddSingleton<FleetDataService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FleetDataService>());

// Per-circuit scoped services (each browser tab gets its own instance)
builder.Services.AddScoped<PlaybackState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("=== MassFleetTracker Dashboard ===");
Console.WriteLine("100k Vehicle Visualization");
Console.WriteLine("Open http://localhost:5000 in your browser");

app.Run();
