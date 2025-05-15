using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoreThanCode.AFrameExample.Database;
using MoreThanCode.AFrameExample.Walk;
using Oakton;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
const string dogwalking = "DogWalking";
var connectionString = builder.Configuration.GetConnectionString(dogwalking) ??
                       throw new NullReferenceException($"ConnectionString {dogwalking} should exists");
builder.Services.AddOpenApi()
    .AddSqlServer<DogWalkingContext>(connectionString)
    .AddWolverineHttp()
    .AddScoped<Watermark>();

builder.UseWolverine(options =>
{
    options.UseEntityFrameworkCoreTransactions();
    options.PersistMessagesWithSqlServer(connectionString, "wolverine");
    options.UseSystemTextJsonForSerialization(serializerOptions => serializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
    options.Policies.AutoApplyTransactions();
    options.Policies.UseDurableLocalQueues();
    options.Policies.ConfigureConventionalLocalRouting();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapWolverineEndpoints();

app.MapGet(
        "/dog/{dogId}",
        async (int dogId, [FromServices] DogWalkingContext db) => Results.Json(await db.Dogs.FindAsync(dogId)))
    .WithName("GetDog");

app.MapPost(
        "/dog",
        async ([FromBody] CreateDog dog, [FromServices] DogWalkingContext db) =>
        {
            var existingDog = await db.Dogs.FirstOrDefaultAsync(d => d.Name == dog.Name && d.Birthday == dog.Birthday);

            var dogCreation = Dog.CreateDog(dog, existingDog);

            switch (dogCreation)
            {
                case DogCreated created:
                    db.Dogs.Add(created.Dog);
                    await db.SaveChangesAsync();
                    return Results.Created($"/dog/{created.Dog.Id}", created.Dog);
                case DogExists exists:
                    return Results.Redirect($"/dog/{exists.Id}");
            }

            return Results.InternalServerError("Could not determine what to do with the dog");
        })
    .WithName("CreateDog");

await app.RunOaktonCommands(args);

internal record CreateDog(string Name, DateOnly Birthday);

public class Dog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateOnly Birthday { get; set; }
    public ICollection<WalkWithDogs> Walks { get; set; }

    internal static DogCreation CreateDog(CreateDog dog, Dog? existing)
    {
        if (existing is not null)
            return new DogExists(existing.Id);

        return new DogCreated(
            new Dog
            {
                Name = dog.Name,
                Birthday = dog.Birthday
            });
    }
}

public record DogResponse(int Id, string Name, DateOnly Birthday)
{
    public DogResponse(Dog dog) : this(dog.Id, dog.Name, dog.Birthday) { }
};

internal abstract record DogCreation;

internal record DogCreated(Dog Dog) : DogCreation;

internal record DogExists(int Id) : DogCreation;