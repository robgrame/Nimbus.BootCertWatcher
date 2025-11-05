using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootWatcher.Shared.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SecureBootDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddHealthChecks();

builder.Services.Configure<FileReportStoreOptions>(builder.Configuration.GetSection("Storage:File"));
builder.Services.AddScoped<EfCoreReportStore>();
builder.Services.AddScoped<FileReportStore>();
builder.Services.AddScoped<IReportStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = configuration.GetValue<string>("Storage:Provider") ?? "EfCore";

    return provider.Equals("File", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<FileReportStore>()
        : sp.GetRequiredService<EfCoreReportStore>();
});

// Configure Azure Queue Processor
builder.Services.Configure<QueueProcessorOptions>(builder.Configuration.GetSection("QueueProcessor"));
builder.Services.AddHostedService<QueueProcessorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
