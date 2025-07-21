using System.Text.Json;
using System.Text.Json.Serialization;
using NetVox.Core;
using NetVox.Core.Interfaces;
using NetVox.Core.Services;
using NetVox.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register backend services
builder.Services.AddSingleton<IConfigRepository, JsonConfigRepository>();
builder.Services.AddSingleton<INetworkService, NetworkService>();
builder.Services.AddSingleton<IPduService>(sp =>
    new PduService(sp.GetRequiredService<INetworkService>()));
builder.Services.AddSingleton<AudioCaptureService>();
builder.Services.AddSingleton<IRadioService>(sp =>
    new RadioService(
        sp.GetRequiredService<AudioCaptureService>(),
        sp.GetRequiredService<IPduService>()));

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
