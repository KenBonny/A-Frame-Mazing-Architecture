using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoreThanCode.AFrameExample.Database;
using Wolverine;
using Wolverine.Http;

namespace MoreThanCode.AFrameExample.Walk;

public record FriendsResponse(string[] Friends, byte[] PictureOfFriends);

public class MetFriendsHandler
{
    public async
        Task<(WalkWithDogs? Walk, List<WalkWithDogs> OtherWalks, Func<byte[]> GetPictureAsync, DateTimeOffset Now)>
        LoadAsync(int walkId, DogWalkingContext db)
    {
        var walk = await db.WalksWithDogs.Include(w => w.Dogs).FirstOrDefaultAsync(w => w.Id == walkId);
        var dogsInWalk = walk?.Dogs.Select(d => d.Id).ToArray() ?? [];
        var otherWalks = await db.WalksWithDogs.Include(w => w.Dogs)
            .Where(w => !w.Dogs.Any(d => dogsInWalk.Contains(d.Id)))
            .ToListAsync();
        var getPicture = () =>
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MoreThanCode.AFrameExample.Yuna.jpg");
            if (stream == null)
                return [];
            stream.Seek(0, SeekOrigin.Begin);
            byte[] image = new byte[stream.Length];
            stream.ReadExactly(image);
            return image;
        };
        return (walk, otherWalks, getPicture, DateTimeOffset.Now);
    }

    public ProblemDetails Validate(WalkWithDogs? walk) =>
        walk is null
            ? new ProblemDetails
            {
                Status = (int)HttpStatusCode.NotFound,
                Title = "Not Found",
                Detail = "Could not find the referenced walk"
            }
            : WolverineContinue.NoProblems;

    [WolverineGet("/friends/{walkId}", OperationId = "Friends-On-Walk")]
    [Tags("MoreThanCode.AFrameExample")]
    public (IResult, OutgoingMessages) Handle(
        WalkWithDogs walk,
        List<WalkWithDogs> otherWalksAtSameTime,
        Func<byte[]> getPicture,
        DateTimeOffset now)
    {
        var outgoingMessages = new OutgoingMessages();

        if (!otherWalksAtSameTime.Any())
            return (Results.Empty, outgoingMessages);

        var friends = otherWalksAtSameTime.SelectMany(w => w.Dogs).Except(walk.Dogs).Select(d => d.Name).ToArray();
        if (friends.Length != 0)
            outgoingMessages.Add(new MetFriends(friends));

        FriendsResponse response = friends.Length == 0 ? new([], []) : new(friends, getPicture());

        return (Results.Ok(response), outgoingMessages);
    }
}

public record MetFriends(string[] Friends);