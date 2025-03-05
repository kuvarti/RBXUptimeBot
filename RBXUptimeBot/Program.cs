using log4net;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<MongoDBSettings>(
	builder.Configuration.GetSection("MongoDBSettings"));
builder.Services.AddSingleton(typeof(IMongoDbService<>), typeof(MongoDbService<>));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Hosted Service (Opsiyonel: Controller dışında servis kullanımı için)
//builder.Services.AddHostedService<StartupHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	AccountManager.isDevelopment = true;
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

AccountManager.AccManagerLoad();
Console.CancelKeyPress += (sender, eventArgs) => AccountManager.ExitProtocol();// ctrl c
AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => AccountManager.ExitProtocol();// regular exit

app.Run();