# A-Frame-Mazing Architecture

## Table of Content

1. What is A-Frame Architecture?

## What is A-Frame Architecture?

A-frame architecture is pretty simple: it separates interfacing with infrastructure from taking decisions using logic. Between the two is a controller who orchestrates the flow of data and information. Everything in your code should respect that separation.

![A-Frame Architecture triangle.png](A-Frame%20triangle.png "A-Frame Architecture triangle")

The idea behind this separation is that each component is easy to reason about, does not influence the other and can thus be easily changed, tested and replaced. Let's look at each in more detail.

### Infrastructure

Infrastructure code (I can refer to this as infrastructure or infra for short throughout this document) interacts with external systems, state and functions with an unpredictable outcome. Examples of the kind of code:
- Database calls
- Send requests over the network
- Read or write to the file system
- Environment variables
- Get the date and time
- Generate random numbers, ids or guids

This list is not exhaustive, but it gives you an idea what infrastructure code looks like. This code can be complicated and hard to test. I find it hard to test whether file access works without accessing the file system. These interactions are very hard to mock or replace, be it in automated tests or manually verifying behaviour. That is why I do not like to mix them in with logic code.

That is why I like to wrap these systems in an abstraction that I can more easily control. Sometimes I create my own interfaces and implementations. For file system access I mostly always create an `IFileSystem` interface that have `Read` and `Write` methods, sometimes with deserialisation baked in. When there are good abstractions already available, I reuse those. [`IOptions`](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) is very useful for accessing settings and [`TimeProvider`](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview) is a great way to abstract time management.

When it comes to database access, there is the [repository pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design#the-repository-pattern). This is a good option when you access the database directly, for example when you use [ADO.NET](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/ado-net-overview) or [Dapper](https://www.learndapper.com). I do not recommend using the repository pattern together with [entity framework](https://learn.microsoft.com/en-us/aspnet/entity-framework) for the simple reason that a `DbContext` already is a repository. This will lead to too much indirection and it leads to duplication of logic. A lot of repositories on top of entity framework start with bespoke methods such as `DogRepository.GetDog(int id)`, but I have seen many times that eventually methods such as `DogRepository.Get(Expression<Func<Dog>> where)` are added for _"flexibility"_. It's a lot easier to just pass in the `DbContext` and use entity framework features directly.

When another developer sees that, I get mostly 2 questions:
1. What about reusing the query? In my experience, most queries within one system are quite unique. A lot cannot and should not be reused. In the few cases where there is reuse possible, don't do it. With duplication of the query, both places can evolve when changes are required without impacting the other locations a query is used. This prevents methods like `DogRepository.GetList(Breed breed, DateRange? bornBetween, int? ownerId)`. This will get a list of dogs by breed. There are optional parameters for a date range so you can specify when the dog was born and a paremeter to specify the owner. I've noticed that those optional parameters are used once each. This results in more complex code to make sure the optional parameters are applied only when supplied. Having an easy `DbContext.Where` in each case, would make life much more easy. For the cases that can be reused, I recommend extension methods. For example a method that filters by breed. This promotes composing different filters into a readable pipeline: `dbContext.IsBreed(Breed.JackRussel).Where(dog => dog.OwnerId == ownerId).ToListAsync()`.
2. This makes testing really hard! No it does not. Instead of creating a mock and telling it to return a specific result, I either use a real database or I use the in-memory database provider. The real database should be set up in a testing pipeline so that it can run anywhere: your machine, the new dev's machine and the CI/CD pipeline. For simple scenarios where I only need to return some data, I use the [in-memory database provider](https://learn.microsoft.com/en-us/ef/core/providers/in-memory). I do not recommend this as an alternative, especially for complex queries. In simple scenarios this mostly does what you expect it to do.

Keep infrastructure simple and straightforward. I prefer to have them as standalone components that do one thing and one thing well. For example: a `FileSystem.Write(string text)` method should serialise the text to bytes and then write it. It should not be changing the markdown inside the text to html first. Create a seperate formatter and chain those components into a pipeline.

In infrastructure code, observability is your best friend. No matter how much you prepare and test, the real system will throw curveballs your way. That is why all infrastructure should be written with [OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) integration so you can track requests throughout systems.

### Logic

Now that we have discussed how to load data from different sources, it's time to make a decision based on that information. This is where logic code comes into play. In some contexts this can be referred to as business logic, but to keep it applicable in more scenarios, I'm going with the more generic term: logic.

Logic code is, prefferably, a pure function. It takes the data it needs as input and outputs the decisions it has made. Most logic code is going to be quite easy to read and understand. The most important rule of logic code, is that it cannot access external systems.

The decisions are input for the infrastructe: save data to a database or file system, notify external systems and post messages to a message bus. I make an exception for logging as this is sometimes closely coupled with logic flow. Even here, we could return the log events as part of the logic code output and have the infrastructure take care of writing to the logstream. Personally, I find that adding logs into the logic code is no less distracting than returning the log events, so I keep my logging code inside the logic code. I have noticed that I write less logs as I log the output of the logic code, which tells me a lot in most cases.

When I need external libraries in my logic code, I inject them together with the data into the function. If the library is simple, I reference it directly. If it is more complex, I hide it behind an interface that narrows the surface of the library to only the parts that I need. For example: if I need to add a watermark to an image, I can use a 3rd party library to help me. Most image processing libraries are quite extensive, so I create a class `Watermark` with a single function `Add`. I can then either inject it if construction is complex or I can instantiate it in my logic code if it's simple enough. The chance that I'll need to substitute the class with another implementation is small, so I don't bother with an interface and sometimes even dependency injection.

The first comment you might have here is: but what if we need to reuse it? Then there are two options: either it does exactly what it needs to in both cases and I can reuse the component without touching it... or it doesn't and the other place needs its own implementation. It is easier to maintain a few similar looking pieces of code than it is to have one class that is shared over my codebase that caters to similar functionality but not quite. The one class will become hard to understand, complex and bloated with all the different pieces of logic it needs in each different case. When one place needs a change, you need to take all the other usecases into consideration before you can make the change. That is why I like to keep this kind of logic spread out throughout the codebase.

Mind you, if exactly the same code pops up all over the place, then that is a good moment to reflect and refactor that into a shared class. The difference is that you see duplication and get rid of it, instead of anticipating what will be reused and creating complexity.

### Controller

Great, now we have code that can load and save the data and there is code to take decisions and make computations. Unfortunately, both cannot know of eachother. That is where the controller comes into play. The controller will pass information from one to another and make sure that the correct data is passed to the correct funtionality.

The controller will instruct the infrastructure code to fetch data from the database, pass it on to the logic code and then tell the database class to persist the changes and tell the message bus to send the messages that the logic code produced.

This code is fairly straightforward and can even be automated away.

## The project

Before I show the code and how it's structured, let me quickly explain the premise of the app. This app will help dog walkers log walks with their dogs and automatically identify who they encountered on their way. Given the other party also entered their route. So I will build a system to keep track of dogs and their owners, the walks they have taken and who they encountered on their walk.

Seeing as this is a demo app, I will use a very simple coordinate system instead of GPS data. I will also assume that all walks happen at the same time. This will simplify the code a bit and keep the focus on the techniques I want to demonstrate instead of going into unnecessary details of the _"business domain"_.

I'm also skipping some unnecessary in-depth explanations. I'm going to assume that you are familiar with setting up Entity Framework or how to correctly use `HttpClient` because you are reading an article about advanced architecture. There are numerous articles that explain those topics in more depth. This article is to highlight the A-Frame Architecture.

## A simple scenario

Let's take a look at how the code is structured. To start out, I will use a minimal API to demonstrate the basic idea of A-Frame Architecture. In examples to come, I'm going to use a framework to connect infrastructure to logic.

The first endpoint that I will create, will create a dog in the database. Let's first take a look at the infrastructure code, this will feel most recognizable.

```csharp
record CreateDog(string Name, DateOnly Birthday);
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
```

The minimal API takes in a structure that was in the body of the request and a connection to the database. It looks the possible already existing dog up in the database. It then lets the `Dog` class take care of creation workflow. I'll get to that in a second. Have some patience, good things come to those who wait.

After the dog creation logic has run, I have to process that result. In the case that the dog is new, it needs to be saved to the database and return the created record. When the dog already exists, I redirect to the place where the consumer can find more information about the dog. Just in case, I return an error should something go wrong.

Now that we have the infrastructure in place, let's look at the logic to create a dog in our system.

```csharp
abstract record DogCreation;
record DogCreated(Dog Dog) : DogCreation;
record DogExists(int Id) : DogCreation;

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
```

Logic is in a lot of cases straightforward when infrastructure concerns are remove. I did create a simple hierarchy to return all the possible outcomes back to the infrastructure layer. I like this little pattern, it reminds me of [discriminated unions/sum type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions). They come from functional programming and [C# might get them too](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md). This is the closest I can get to that for the moment, I like this because it describes all the possible outcomes that can come out of the `CreateDog` function. This is also what the infrastructure code uses to determine what needs to be done.

What I also could have done, was to create a single property `Dog` on the base class and passed the created or existing dog reference back to the infrastructure. I like to return only what is needed back to my infrastructure. The logic code does not need to know whether a created or redirect needs to be sent over the wire. I, as the maintainer of the system, do not want to return too much so I can easily process the result. The last reason I do this, is to highlight that each case has its own needs and should not contain any more than needed. The whole purpose of this architecture is to simplify code.

Automated tests can very easily verify whether the logic takes the right decisions. There is no infrastructure code that gets in the way. The "messy" infrastructure code with all of its side effects can be better tested with integration tests. That way, all infrastructure code gets verified in a few scenarios, while all the special logic cases get their own (much faster) unit tests.

For a simple situation like this, this can feel overkill. The point here is to showcase how separation between infrastructure and logic can make code a lot more understandable. Even without knowing what `CreateDog` function did, the returned values describe perfectly what you can expect and how the infrastructure should respond to it.

The readers who have been paying attention, noticed that the controller and infrastructure logic have interwoven in this example. Congratulations to you who spotted it. In this case, I don't mind as this is simple enough as a first example. A lot of infrastructure code is hidden behind the Entity Framework abstraction, so there is the case to be made that it's still separated well enough.

## An even simpler scenario

The processing was easy enough to understand. I returned a reference to an endpoint that loads the details of a dog. How does that look in our A-Frame Architecture. Wel, I wouldn't use A-Frame Architecture for this. Most queries are so straightforward, that I don't want to bother with abstractions or indirection. I would just use a very, and I mean very, simple approach.

```csharp
app.MapGet(
        "/dog/{dogId}",
        async (int dogId, [FromServices] DogWalkingContext db) => Results.Json(await db.Dogs.FindAsync(dogId)))
    .WithName("GetDog");
```

Even if queries get more complicated, most don't reach the level of processing code. I place these in a separate file with a single function. There is the occasional exception that breaks this rule, but for those cases I'd look for a bespoke approach to the problem at hand instead of a one-size-fits-all approach that I see in other code bases.

Now that the basics are highlighted, let's take a look at how I can make my life a lot easier with a framework that already does a lot of the heavy lifting.

## Wolverine

[Wolverine](https://wolverine.netlify.app) is focused on messaging but goes beyond what other messaging frameworks offer. You can start in-memory and then integrate a bus (RabbitMQ, Azure Service Bus, Amazon SQS,...) when you need to distribute computing. Every step can make use of quality of life features such as durable messaging (in- and outbox patterns), retries, timeouts, error handling and everything else Wolverine has to offer. It doesn't stop there as HTTP requests can be handled as messages too. After all, any HTTP request is just a message that comes in through the web instead of through a bus.

Integration with existing projects can be done gradually, as Wolverine can be installed alongside other messaging and API frameworks such as MassTransit, NServiceBus, minimal API's, ASP.NET or Blazor. So you can mix and match as you like or gradually migrate from one to another. For this purpose, I will leave the functionality of creating and retrieving a dog in the minimal API.

I will use Wolverine to create a walk in the system. I want to tell the system with which dogs I walked from point to point. In a real system, I might use the geolocation from the phone to capture GPS points and add them when the app detects that a walk has been started. For this example, I'm going to use a simple coordinate system ([0,0], [0,1], [2, 1], etc.) to indicate the walk. I also want to verify that the dogs that go on this walk are known in the system.

```csharp
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
    public (LazyCreationResponse<WalkResponse> response, EntityFrameworkInsert<WalkWithDogs> insertWalk) Handle(
        RegisterWalk request,
        Dog[] dogsOnWalk)
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

        var response = () => new WalkResponse(walk.Id, dogsOnWalk.Select(d => new DogResponse(d)).ToArray(), request.Path);
        return (LazyCreationResponse.For(() => $"/walk/{walk.Id}", response),
            new EntityFrameworkInsert<WalkWithDogs>(walk));
    }
}
```

Wolverine works by convention: first it loads data when it sees a `Load` or `LoadAsync` function, then it validates the request and finally it processes the `Handle` or `Consume` function. The `Load` function is equivalent to the infrastructure code and the `Handle` function is our logic code. The `Validate` function is a bonus to even further separate logic.

This framework is smart enough to inject the required services into each function. It will get the `RegisterWalk` object from the HTTP body and the `DogWalkingContext` from DI services. What the infrastructure code returns can be injected into the `Validate` and `Handle` functions. If I need to return multiple pieces of data from the infrastructure, I can return a tuple. The tuple will be nicely broken down, and each item is injected separately in the following functions.

What I find impressive, is that Wolverine can handle several things that are returned by the `Handle` function. In an HTTP handler, the first item in the returned tuple is what will be returned to the client. The next items will be either posted as messages on the configured bus or handled as a side effect. A side effect is anything related to infrastructure: saving contents to a file, updating rows in the database, making network calls,... It makes this distinction by looking for the presence of the `ISideEffect` interface.

## A more advanced example

This seems really nice for simple cases, yet what happens when there are more advanced usecases. The two big scenarios are:
1. I need to perform an infrastructure call in the middle of logic code.
2. I need to do different infrastructure calls based on the decisions taken by my logic code

### 1. Multiple pieces of infrastructure data

The easiest thing to do is to avoid these. Try to structure the logic code differently so that the infrastructure code can load all the data that is needed. When multiple objects need to be loaded, the `Load` method can return a tuple. Each item can be separately injected in the `Before`, `Validate` and `Handle` methods. So loading data from a database, an image from the web and the current date from the system can be returned.

```csharp
public async Task<(List<Dog> dogs, Image picture, DateTimeOffset now)> LoadAsync(/* dependencies go here */) 
{
    // infrastructure code goes here
    return (dogsFromDatabase, pictureFromTheWeb, DateTimeOffset.Now);
}

public ProblemDetails Validate(List<Dog> dogs, DateTimeOffset now)
{
    // validation code goes here
    return WolverineContinue.NoProblems;
}

public DogDto Handle(List<Dog> dogs, Image picture, DateTimeOffset now, IWatermarkService watermark)
{
    // logic code goes here
}
```

Wolverine will inject the dog list, the picture and the date correctly into the `Validate` and `Handle` methods. It will even resolve the `IWatermarkService` from the dependency injection framework.

### 2. Infrastructure calls in the middle of logic code

That is all nice and dandy, but the use case I have in mind cannot be preloaded. Let's say there is a heavy data load that I do not want to incur upfront. The picture is an 8k image and depending on some business logic, I might not need the picture. All that waiting and wasted compute for nothing. One way is to inject the infrastructure in the logic code and be done with it. Unfortunately, that means I'm back to square one with all the problems of mixing infra and logic code.

Luckily, `Func<>` is an object that I can return from the `Load` method. Thus delegating the execution to a later time. I get the benefits that the infrastructure code prepares the data, and I get an immutable way of testing my logic as I can replace the `Func<>` with a simple test stub.

```csharp
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

[WolverineGet("/friends/{walkId}", OperationId = "Friends-On-Walk")]
[Tags("MoreThanCode.AFrameExample")]
public IResult Handle(
    WalkWithDogs walk,
    List<WalkWithDogs> otherWalksAtSameTime,
    Func<byte[]> getPicture,
    DateTimeOffset now)
{
    if (!otherWalksAtSameTime.Any())
        return Results.Empty;

    var friends = otherWalksAtSameTime.SelectMany(w => w.Dogs).Except(walk.Dogs).Select(d => d.Name).ToArray();

    FriendsResponse response = friends.Length == 0 
        ? new([], []) 
        : new(friends, getPicture());

    return Results.Ok(response);
}
```

### 2. Different return signatures



## Sources

[A-Frame Architecture with Wolverine](https://jeremydmiller.com/2023/07/19/a-frame-architecture-with-wolverine/)
[James Shore A-Frame Architecture](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks#a-frame-arch)