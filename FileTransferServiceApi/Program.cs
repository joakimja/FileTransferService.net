var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//app.MapGet("/", () => "Hello World!");

app.MapGet("/files", (int id) =>
{
    
    return Results.NoContent();
});

app.Run();
