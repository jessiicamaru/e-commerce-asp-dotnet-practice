var builder = WebApplication.CreateBuilder(args);

// Add YARP Reverse Proxy services and load configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Enable YARP Reverse Proxy middleware routing
app.MapReverseProxy();

app.Run("http://localhost:5000");
