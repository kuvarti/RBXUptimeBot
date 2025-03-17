using log4net;
using Microsoft.EntityFrameworkCore;
using RBXUptimeBot.Classes;
using RBXUptimeBot.Classes.Services;
using RBXUptimeBot.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PostgreService>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

AccountManager.AccManagerLoad(builder.Configuration.GetConnectionString("DefaultConnection"));
Console.CancelKeyPress += (sender, eventArgs) => AccountManager.ExitProtocol();// ctrl c
AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => AccountManager.ExitProtocol();// regular exit

app.Run();