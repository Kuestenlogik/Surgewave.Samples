using Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Components;
using Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add Blazor components with interactive server rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Fleet Tracker services
// MessageBuffer is a singleton shared by all clients - stores all consumed messages
builder.Services.AddSingleton<MessageBuffer>();

// FleetDataService consumes messages into the buffer
builder.Services.AddSingleton<FleetDataService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FleetDataService>());

// ClientPlaybackState is scoped per circuit - each browser gets its own playback controls
builder.Services.AddScoped<ClientPlaybackState>();

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

Console.WriteLine("=== Fleet Tracker Dashboard ===");
Console.WriteLine("Open http://localhost:5000 in your browser");

app.Run();
