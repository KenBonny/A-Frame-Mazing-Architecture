using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoreThanCode.AFrameExample.Database;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi()
    .AddSqlServer<DogWalkingContext>(builder.Configuration.GetConnectionString("DogWalking"))
    .AddWolverineHttp();

builder.UseWolverine(options =>
{
    options.Policies.AutoApplyTransactions();
    options.Policies.UseDurableLocalQueues();
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

app.Run();

record CreateDog(string Name, DateOnly Birthday);

public class Dog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateOnly Birthday { get; set; }

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

abstract record DogCreation;
record DogCreated(Dog Dog) : DogCreation;
record DogExists(int Id) : DogCreation;