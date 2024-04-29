using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UTFClassAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "UTF Class API", Version = "v1" });
});

// Add database service EFCore
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlite("Data Source = file:.\\Data\\Database.sqlite3;Mode=ReadWrite;")
           .EnableSensitiveDataLogging());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = string.Empty;
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UTF Class API");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();