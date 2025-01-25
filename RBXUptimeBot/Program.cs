using log4net;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<MongoDBSettings>(
	builder.Configuration.GetSection("MongoDBSettings"));
builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();

	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

AccountManager.AccManagerLoad();

app.Run();
