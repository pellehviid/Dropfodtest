using System.Device.I2c;
using Iot.Device.Imu;
using Iot.Device.Mpu6050;
using Microsoft.AspNetCore.SignalR;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
var app = builder.Build();

app.MapHub<SensorHub>("/sensorHub");

// --- Her læser vi nu fra din eksterne fil ---
app.MapGet("/", async () =>
{
    // AppDomain finder stien til mappen hvor din .exe/.dll fil ligger
    var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");

    if (!File.Exists(filePath))
    {
        return Results.Text($"Fejl: index.html blev ikke fundet på stien: {filePath}");
    }

    var html = await File.ReadAllTextAsync(filePath);
    return Results.Content(html, "text/html");
});

_ = Task.Run(async () =>
{
    var settings = new I2cConnectionSettings(1, 0x68);
    using var i2c = I2cDevice.Create(settings);
    using var sensor = new Mpu6050(i2c);
    var hub = app.Services.GetRequiredService<IHubContext<SensorHub>>();

    while (true)
    {
        try
        {
            var acc = sensor.GetAccelerometer();
            double p = Math.Atan2(-acc.X, Math.Sqrt(acc.Y * acc.Y + acc.Z * acc.Z)) * (180 / Math.PI);
            double g = Math.Sqrt(acc.X * acc.X + acc.Y * acc.Y + acc.Z * acc.Z) / 9.81;

            await hub.Clients.All.SendAsync("ReceiveData", p.ToString("F1"), g.ToString("F2"));
            Console.WriteLine($"Pitch: {p:F1}° | G: {g:F2}");
        }
        catch (Exception ex)
        {
            // Det er altid godt at logge fejlen, hvis I2C falder ud
            Console.WriteLine($"Sensor fejl: {ex.Message}");
        }

        await Task.Delay(100);
    }
});

app.Run("http://0.0.0.0:5000");

public class SensorHub : Hub { }