using Microsoft.Extensions.Configuration;
using PortManager;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string confName = builder.Configuration.GetValue<string>("WireGuard:ConfigName") ?? string.Empty;
if (string.IsNullOrWhiteSpace(confName))
{
    throw new Exception("WireGuard 配置名称未设置，无法启动应用程序。");
}
builder.Services.AddHostedService<PortService>();
builder.Services.AddScoped<IPortService, PortService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
