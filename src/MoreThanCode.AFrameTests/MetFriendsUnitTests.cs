using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoreThanCode.AFrameExample.Walk;
using Wolverine;
using Wolverine.Http;

namespace MoreThanCode.AFrameTests;

public class MetFriendsUnitTests
{
    private readonly MetFriendsHandler _friendsHandler = new();

    private readonly WalkWithDogs _walk = new()
    {
        Dogs =
        [
            new Dog
            {
                Name = "Yuna",
                Birthday = new(2021, 5, 12)
            }
        ],
        Path =
        [
            new()
            {
                X = 1,
                Y = 1,
                SequenceOrder = 1
            },
            new()
            {
                X = 1,
                Y = 2,
                SequenceOrder = 2
            },
            new()
            {
                X = 2,
                Y = 2,
                SequenceOrder = 3
            },
            new()
            {
                X = 2,
                Y = 1,
                SequenceOrder = 4
            },
            new()
            {
                X = 1,
                Y = 1,
                SequenceOrder = 5
            }
        ]
    };private readonly WalkWithDogs _otherWalk = new()
    {
        Dogs =
        [
            new Dog
            {
                Name = "Toby",
                Birthday = new(2022, 2, 21)
            }
        ],
        Path =
        [
            new()
            {
                X = 2,
                Y = 2,
                SequenceOrder = 3
            }
        ]
    };

    [Test]
    public async Task A_known_walk_is_valid()
    {
        var problemDetails = _friendsHandler.Validate(new WalkWithDogs
        {
            Dogs = [],
            Path = []
        });

        await Assert.That(problemDetails).IsEqualTo(WolverineContinue.NoProblems);
    }
    
    [Test]
    public async Task An_unknown_walk_is_invalid()
    {
        var problemDetails = _friendsHandler.Validate(null);

        await Assert.That(problemDetails)
            .IsEquivalentTo(
                new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = "Could not find the referenced walk"
                });
    }

    [Test]
    public async Task When_no_other_dog_encountered_then_do_nothing()
    {
        var getPictureCalled = false;
        var (result, outgoingMessages, entityFrameworkInsert) = _friendsHandler.Handle(
            _walk,
            [],
            () =>
            {
                getPictureCalled = true;
                return [];
            },
            DateTimeOffset.Now);

        await Assert.That(result).IsEqualTo(Results.Empty);
        outgoingMessages.ShouldHaveNoMessages();
        await Assert.That(entityFrameworkInsert).IsNull();
        await Assert.That(getPictureCalled).IsFalse();
    }

    [Test]
    public async Task When_other_dog_encountered_then_do_indicate_dog_encountered()
    {
        var getPictureCalled = false;
        var (result, outgoingMessages, entityFrameworkInsert) = _friendsHandler.Handle(
            _walk,
            [_otherWalk],
            () =>
            {
                getPictureCalled = true;
                return [];
            },
            DateTimeOffset.Now);

        await Assert.That(result).IsEquivalentTo(Results.Ok(new FriendsResponse(["Toby"], [])));
        var friends = outgoingMessages.ShouldHaveMessageOfType<MetFriends>().Friends;
        await Assert.That(friends).IsNotEmpty().And.Contains("Toby");
        await Assert.That(entityFrameworkInsert).IsNotNull();
        await Assert.That(getPictureCalled).IsTrue();
    }
}