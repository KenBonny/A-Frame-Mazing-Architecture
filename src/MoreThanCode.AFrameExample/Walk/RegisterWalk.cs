using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoreThanCode.AFrameExample.Database;
using Wolverine.Http;
using Wolverine.Persistence;

namespace MoreThanCode.AFrameExample.Walk;

public record struct Coordinate(int X, int Y);

public record RegisterWalk(string[] DogsOnWalk, Coordinate[] Path);

public class RegisterWalkHandler
{
    public Task<Dog[]> LoadAsync(RegisterWalk request, DogWalkingContext db) =>
        db.Dogs.Where(d => request.DogsOnWalk.Contains(d.Name)).ToArrayAsync();

    public ProblemDetails Validate(RegisterWalk request, Dog[] knownDogs)
    {
        var knownNames = knownDogs.Select(d => d.Name);
        var unknownDogs = request.DogsOnWalk.Except(knownNames).ToArray();
        return unknownDogs.Any()
            ? new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Unknown dog or dogs",
                Detail = string.Join(", ", unknownDogs)
            }
            : WolverineContinue.NoProblems;
    }

    [WolverinePost("/walk", Name = "Register Walk", OperationId = "Walk")]
    [Tags("MoreThanCode.AFrameExample")]
    public (IResult, Insert<WalkWithDogs> insertWalk) Handle(RegisterWalk request, Dog[] dogsOnWalk)
    {
        var walk = new WalkWithDogs
        {
            Dogs = dogsOnWalk,
            Path = request.Path.Select((coord, index) => new CoordinateEntity
                {
                    X = coord.X,
                    Y = coord.Y,
                    SequenceOrder = index
                })
                .ToList()
        };

        var result = new WalkResponse(walk.Id, dogsOnWalk.Select(d => new DogResponse(d)).ToArray(), request.Path);
        return (Results.Created($"/walk/{walk.Id}", result), Storage.Insert(walk));
    }
}

public class CoordinateEntity
{
    public int Id { get; set; }  // Primary key
    public int X { get; set; }
    public int Y { get; set; }
    public int WalkId { get; set; }  // Foreign key
    public WalkWithDogs Walk { get; set; } = null!;  // Navigation property
    public int SequenceOrder { get; set; }  // To maintain the sequence of coordinates
}

public class WalkWithDogs
{
    public int Id { get; init; }
    public required ICollection<Dog> Dogs { get; init; }
    public required ICollection<CoordinateEntity> Path { get; init; }
}

internal record WalkResponse(int Id, DogResponse[] Dogs, Coordinate[] Path);

public record WalkedWithDogs
{
    public required Dog[] Dogs { get; init; }
    public required Coordinate[] Path { get; init; }
}