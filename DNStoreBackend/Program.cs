using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using DNStoreBackend.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<DNStoreDB>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Allow large uploads
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500_000_000;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500_000_000;
});

var app = builder.Build();

app.UseStaticFiles();

app.MapControllers();

app.Run();