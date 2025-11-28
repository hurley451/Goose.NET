using Goose.Core.Abstractions;
using Goose.Core.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddLogging();

// Register core services  
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddTransient<IConversationAgent, ConversationAgent>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

// Configure CORS based on environment
if (app.Environment.IsDevelopment())
{
    // Development: Allow all origins for easier testing
    app.UseCors(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
}
else
{
    // Production: Restrict to configured origins
    app.UseCors(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Fallback: No CORS if not configured (secure default)
            policy.AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
}

app.MapControllers();

app.Run();
