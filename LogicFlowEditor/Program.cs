using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LogicFlowEditor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<LogicFlowEditor.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<GraphStateService>();
builder.Services.AddSingleton<SimulationService>();
builder.Services.AddSingleton<GraphSerializer>();

// Register all built-in engine node types
NodeRegistry.RegisterBuiltins();

await builder.Build().RunAsync();
