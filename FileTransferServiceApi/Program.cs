var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
//get files
app.MapGet("/files", (int id) =>
{
    
    return Results.NoContent();
});

app.Run();
