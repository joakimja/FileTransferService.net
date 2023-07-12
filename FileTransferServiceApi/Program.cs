using System.IO;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
//get files
app.MapGet("/files", (int id) =>
{
    StreamReader sr = new StreamReader("C:\\basic.txt");
    return Results.NoContent();
});

app.Run();
