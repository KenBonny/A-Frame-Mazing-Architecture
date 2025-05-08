# A-Frame-Mazing Architecture

## Table of Content

1. The project
2. What is A-Frame Architecture?
    1. Infrastructure
    2. Logic
    3. Controller
3. A simple scenario
4. An even simpler scenario
5. Wolverine
6. A more advanced example
   1. Multiple pieces of infrastructure data
   2. Infrastructure calls in the middle of logic code
   3. Complex return instructions
7. Testing
   1. Unit tests
   2. Integration tests
8. Sources

## The project

Before I explain the architecture, let me quickly explain the purpose of the app. This app will help dog walkers log walks with their dogs and automatically identify who they encountered on their stroll. I will build a system to keep track of dogs, their walks and who they met.

Seeing as this is a demo app, I will use a basic coordinate system instead of GPS data. I'll ignore the time the walks happen. This will simplify the code a bit and keep the focus on the techniques instead of going into unnecessary details of the non-existent business domain.

For brevity, I'll assume you are familiar with popular concepts such as setting up entity framework or how to correctly use `HttpClient`. There are numerous articles explaining those topics in more depth. I want to keep A-Frame architeture front and centre.

## What is A-Frame Architecture?

A-frame architecture is pretty simple: it separates interfacing with infrastructure from taking decisions using logic. Between the two is a controller who orchestrates the flow of data. Everything in your code should respect that separation.

![A-Frame Architecture triangle.png](A-Frame%20triangle.png "A-Frame Architecture triangle")

This leads to code components that are nicely separated, have a single responsibility and have no dependencies on other components. This makes each component easy to reason about, which in turn leads to code that can easily be tested, changed and replaced. Infrastructure components tend to be more general and promote reuse, while logic is more specific to each use case.

For example, I can have several logic components that need to write an image to the file system. One will deal with profile pictures while another will handle uploaded photographs. Both will delegate the write operation to the same infrastructure component.

Let's take a look at each in more detail.

### Infrastructure

Infrastructure components (aka infrastructure) interact with external systems, state and functions with an unpredictable outcome. Examples are:
- Database calls
- Sending requests over the network
- Reading or writing to the file system
- Retrieving environment variables
- Determining date and time
- Generating random numbers, ids or guids

These components can be harder to test without an actual system to talk to. They are also challenging to mock or replace, both in automated tests and test environments. Infrastructure can also behave in unpredictable ways: do I have the right permissions or credentials, is there enough space on a disk, can I make the call through the firewall, etc.

That is why I like to wrap these systems in an abstraction that I can more easily control. Sometimes I create my own interfaces and implementations. For file system access I mostly always create an `IFileSystem` interface that have `Read` and `Write` methods, sometimes with (de)serialisation baked in. When there are good abstractions already available, I reuse those. [`IOptions`](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) is invaluable for accessing settings and [`TimeProvider`](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview) is a great way to abstract time management.

When it comes to database access, there is the [repository pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design#the-repository-pattern). This is a good option when you access the database directly with [ADO.NET](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/ado-net-overview) or [Dapper](https://www.learndapper.com). I do not recommend using the repository pattern together with [entity framework](https://learn.microsoft.com/en-us/aspnet/entity-framework) for the simple reason that a `DbContext` already is an abstraction over your database. I've seen this lead to a maze of indirection and duplication of logic. A repository with `Repository.Get(Expression<Func<Model>> where)` that has one implementation which just forwards the `where` to the `DbContext`, comes to mind.

When I suggest removing the unnecessary interface, I'm asked these questions:
1. What about reusing a query? In my experience, most queries are unique. Reused parts can be put into extension methods. Think filtering by a status enum or by a date range. What I'm trying to avoid is a single function with parameters for each case. Think `Search(StatusEnum? status, DateRange? bornBetween, int? idToFilterOn)` with `if`'s throughout the body to filter by each optional parameter. The implementation will get quite complex and confusing. Not to mention the dozens of tests to check that it works in every configuration. What about the UI that filters through all items and has a dozen filters? That should be a feature with its own endpoint and not a method on the generic repository.
2. How do I test against the `DbContext`? Instead of using mocks/fakes/stubs, use a real database. This is what integration tests are used for as there is no substitute for a real database. You can use a Docker container for local test runs, start a database in your CI/CD pipeline or use [Test Containers](https://dotnet.testcontainers.org). See the [[Testing]] section for a detailed explanation.

Keep infrastructure as simple and straightforward as possible. I prefer to have them as standalone components that do one thing and do it well. For example, a `FileSystem.Write(Image image)` should know how to serialise the image to a byte array. If there are multiple ways of serialising, that can be either a logic decision which should be passed to the component or the component should be able to determine which serialiser is needed.

In infrastructure code, observability is your best friend. No matter how much you prepare and test, the real system will throw curveballs your way. That is why all infrastructure should be written with [OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) integration so you can track requests throughout systems.

### Logic

Now that I've loaded data, it's time to make decisions based on that information. This is where logic components come into play. In some contexts this can be referred to as business logic, but to keep it applicable in more scenarios, I'm going with the more generic term: logic.

A logic component is, preferably, a pure function. It takes the data it needs as input and returns the decisions it has made. Most logic components are going to be quite easy to read and understand. The most important rule of logic components is that it cannot access external systems. There are a lot of similarities between logic components and the domain model from domain driven design practices. 

The returned decisions are then passed back to infrastructure components: save data to a database, write it to a file system, notify external systems and post messages to a bus. I'll make an exception for logging as this can be coupled with the logic flow. I could return the log events and write them to a log stream in an infrastructure component. Going this far is overkill, in my opinion. I prefer writing the in- and output of logic components to OpenTelemetry. This keeps the logic free of logging statements and still gives me all the information necessary to debug later.

When I have need of external libraries in my logic code, I inject them after the data `Process(Model model, DateTime now, ImageProcessingLib lib)` or I instantiate it inside the method. If the library is complex, I hide that complexity in a class of my own. Say I need to add a watermark to an image, I'll wrap the extensive image processing library in a class called `Watermark`. Injection or instantiation then depends on how expensive it is to create that class. I don't mind tight coupling if it makes sense. The library does not produce side effects (like storing the image to disk), and I don't need to replace it with a mock in my tests.

This approach lends itself to reuse very easily: inject or instantiate the `Watermark` class and use it. It's even easy to extend to add a timestamp in another feature... Wait, hold that thought. I get why this seems like a good idea, both are adding something to an image. Unfortunately, this is a case where the functionality looks alike, but is quite different in practice. Adding a watermwark is something else than adding a timestamp. They are only accidentally alike. The moment I'd start implementing this, I'd notice they are different. I would take the lessons learned from the `Watermark` implementation and just create another class `Timestamp`. This is easier to maintain, evolve, replace or compose.

It's only when I notice that similar code appears in the codebase that I'll reflect and refactor into a shared class or component. The difference is that I'll react to what is actually there instead of prematurely optimising. This way it's more challenging to create accidental complexity.

### Controller

This is a good point in the development process to think about the last step. I can load the necessary data and act upon it; all that is needed is to connect the dots. This is where the controller comes into play. It will pass information from one to the other and make sure the two never meet. The controller will determine what data to load and pass it on to the logic component. Finally, it will instruct other infrastructure components based on the output of the logic. This code is fairly straightforward and can even be automated away.

## A simple scenario


Now the expected structure is clear, let's take a look at the code. I will start with a minimal API implementation to demonstrate that no frameworks are needed to implement this architecture. I did find that Vertical Slice architecture pairs well with A-Frame architecture. For that purpose, I'll use a framework that makes it easy to connect infrastructure and logic by generating the controller.

The initial endpoint will create a dog in the database. I'll start with the infrastructure code as this will be the most recognisable.

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

The endpoint retrieves a possibly existing dog and passes it, together with the create command, to the `CreateDog` handle function. The result of the dog creation is then handled. When the dog needs to be created, it is saved to the database and a _201 Created_ response is sent back. When the dog already exists, I redirect the client to that resource. In case something goes wrong, I'll return a _500 Internal Server Error_.

Now that I have the infrastructure in place, let's look at the logic to create a dog in our system.

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

If a dog already exists, I return the identifier of that dog. Otherwise, I'll create a new dog. When infrastructure concerns are removed, the remaining logic is straightforward. The return structure is easy to understand and describes all possible outcomes. I like this little pattern, it reminds me of [discriminated unions/sum type](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions). I hope [C# gets them soon](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md) as I think functional programming paradigms are quite elegant.

It's possible to move the `Dog` property to the base record. Then I'd have all information to feed back into my infrastructure. I like to return just what is necessary, thus keeping in line with the [principle of least privilege](https://en.wikipedia.org/wiki/Principle_of_least_privilege). The infrastructure only needs the dogs id to create the redirect. It also means that I don't return the response in the logic code as that would mean the logic would need to know about the infrastructure. These return objects also better explain what the logic decided. The whole purpose of this architecture is to simplify code.

Setting up automated tests for logic components is quite easy as there is no infrastructure that gets in the way. Infrastructure benefits more from integration tests that check all the messy side effects, while all the logic output can be verified with (much faster) unit tests.

The readers who have been paying attention, noticed that the controller and infrastructure code have interwoven in this example. Congratulations to you who spotted it. In this case, I don't mind as this is simple enough as a first example. A lot of infrastructure code is hidden behind the Entity Framework abstraction, so there is the case to be made that it's still separated well enough. In real software I would put every infrastructure instruction into its own method or even class.

## An even simpler scenario

The processing was easy enough to understand. I returned a reference to an endpoint that loads the details of a dog. How does that look in our A-Frame architecture? I wouldn't use A-Frame Architecture for this. Most queries are so straightforward that I don't want to bother with abstractions or indirection. I would just use a very, and I mean very, simple approach.

```csharp
app.MapGet(
        "/dog/{dogId}",
        async (int dogId, [FromServices] DogWalkingContext db) => Results.Json(await db.Dogs.FindAsync(dogId)))
    .WithName("GetDog");
```

Even if queries get more complicated, most don't reach the level of processing code. I'd place these in a separate file with a single function. There is the occasional exception that breaks this rule, but for those cases I'd look for a bespoke approach to the problem at hand instead of a one-size-fits-all approach.

Now that the basics are highlighted, let's take a look at how I can make my life a lot easier with a framework that already does a lot of the heavy lifting.

## Wolverine

[Wolverine](https://wolverine.netlify.app) is at its core a messaging framework, but goes well beyond that. It has an in-memory transport so it behaves like a mediator, but with all the quality of life improvements that apply to any other transport. It supports durable messaging via the in- and outbox patterns, retries, timeouts, error handling and much more. The in-memory transport is great for integration testing as well. Once the app goes live, it can switch the transport out for an external system usch as RabbitMQ, Azure Service Bus, Amazon SQS/SNS, Kafka and even SQL server or Postgres if your messaging needs are limited. It even has support for messages coming in through HTTP requests so I can build an API with it.

Integration with existing projects can be done gradually, as Wolverine can be installed alongside other messaging and API frameworks such as MassTransit, NServiceBus, minimal API's, ASP.NET or Blazor. This allows me to mix and match as I like or gradually migrate from one to another. For this purpose, I will leave the functionality of creating and retrieving a dog in the minimal API.

To create a walk, I'll have to map the path I walked and which dogs I had with me. In a real system, I'd look for GPS integration, for this demo system I'll use a list of coordinates ([0,0], [0,1], [2, 1], etc.). I also want to verify that the dogs are in the system, so I don't have unknown dogs with me.

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

Wolverine works by convention: first it loads data when it sees a `Load` or `LoadAsync` function, then it validates the request and finally it processes the `Handle` or `Consume` function. The `Load` function is equivalent to the infrastructure code and the `Handle` function is my logic code. The `Validate` function is a bonus to even further separate validation from logic.

Through source generation, it creates the controller for me. This means that the code is fast because there is no reflection during runtime. It also figures out what to inject into each function. It can find injectable servcies through the configured dependency injection framework, it can inject messages from the bus and HTTP pipeline (including query parameters and form data) and objects that are returned from the `Load` function. That is how the `Validate` and `Handle` methods get the command and list of dogs.

What I find most impressive, is that Wolverine can handle the output of the `Handle` function. In an HTTP handler, the first item in the returned tuple is the response to the client. The next items will be either posted as messages on the configured bus or handled as a side effect. A side effect is anything related to infrastructure: saving contents to a file, updating rows in the database,... It makes this distinction by looking for the presence of the `ISideEffect` interface. These are the most important features to get started with A-Frame architecture, for more in depth knowledge I'll refer to the [Wolverine docs](https://wolverine.netlify.app/tutorials/).

## A more advanced example

This seems really nice for simple cases, yet what happens when there are more advanced usecases. The two big scenarios are:
1. I need multiple pieces of data from different sources
2. I need to perform an infrastructure call in the middle of logic
3. I need to do different infrastructure calls based on the decisions taken by my logic code

### 1. Multiple pieces of infrastructure data

When multiple objects need to be loaded, the `Load` method can return a tuple. Each item can be separately injected in the `Validate` and `Handle` methods. So loading data from a database, an image from the web and the current date from the system can be returned as a Tuple. Wolverine will inject the list of dogs, the picture and the date correctly into the `Validate` and `Handle` methods. It will even resolve the `IWatermarkService` from the dependency injection framework.

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

### 2. Infrastructure calls in the middle of logic code

The easiest solution is to avoid this scenario. Try to structure the logic code differently so that the infrastructure code can load all the data that is needed upfront. There are situations where I don't want to incur the upfront cost. For example, when there is a chance that the data isn't necessary and the load puts the system under unnecessary stress. In such cases, I can inject infrastructure code into the logic component. This does not need to mean that I'm back to square one of injecting an interface or `DbContext` into my `Handle` method. 

Luckily, `Func<>` can be returned from the `Load` method. Thus delegating the execution to a later time. I get the benefits that the infrastructure code prepares the call, and I get an immutable way of testing my logic as I can replace the `Func<>` with a simple test stub.

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

### 3. Complex return instructions

As a last step, let's look at the return of the function. What if I want to save the walk back to the database, write the picture to disk or publish additional messages to the network? For these purposes, Wolverine has [cascading messages](https://wolverine.netlify.app/guide/handlers/cascading.html) and [`ISideEffect`](https://wolverine.netlify.app/guide/handlers/side-effects.html)s.

Cascading messages can be returned in a variety of ways, but I prefer to work with the `OutgoingMessages` response type. This way I can add none, one or multiple messages based on the decisions of my logic. These messages will be published to the bus according to the routing information defined in Wolverine's configuration. It's also possible to schedule or delay messages if needed. All these messages will benefit from the resiliency that Wolverine has to offer.

When there are infrastructure operations that are part of the scope of the handler, those can be implemented as side effects. Common side effects are saving to the database and writing to disk. For this, there is the `ISideEffect` marker interface that expects an `Execute` function to be available. This function is not present on the interface as the input can accept parameters resolved from dependency injection, just like the `Handle` function discussed earlier. When a side effect is optional, make it nullable and return `null` if it should not happen.

It isn't possible to return a generic list of `ISideEffect`s. Wolverine needs to know the explicit type of the side effect to resolve the injectable parameters correctly. Side effects are applied inside the scope of the transaction where the `Load` and `Handle` function are part of. After everything in the transaction succeeds, then the messages are published. This to prevent ghost messages notifying other parts of the system of an operation that may have failed. To prevent messages from disappearing because there is a problem with the bus, it is recommended to enable the outbox via [durable messaging](https://wolverine.netlify.app/guide/durability/). Storing the message in the database is part of the transaction which prevents those messages from being lost.

````csharp
public (IResult, OutgoingMessages, EntityFrameworkInsert<WalkWithDogs>?) Handle(
    WalkWithDogs walk,
    List<WalkWithDogs> otherWalksAtSameTime,
    Func<byte[]> getPicture,
    DateTimeOffset now)
{
    var outgoingMessages = new OutgoingMessages();

    if (!otherWalksAtSameTime.Any())
        return (Results.Empty, outgoingMessages, null);

    var friends = otherWalksAtSameTime.SelectMany(w => w.Dogs).Except(walk.Dogs).Select(d => d.Name).ToArray();
    if (friends.Length != 0)
        outgoingMessages.Add(new MetFriends(friends));

    FriendsResponse response = friends.Length == 0 ? new([], []) : new(friends, getPicture());

    return (Results.Ok(response), outgoingMessages, new EntityFrameworkInsert<WalkWithDogs>(walk));
}
````

A last remark: when you have side effects that can fail, publish an [business or infrastructure event](). The most prevalent examples are network calls. A network call can fail for a multitude of reasons. I like to leverage built-in [retry mechanisms](https://wolverine.netlify.app/guide/handlers/error-handling.html) and [error handling](https://wolverine.netlify.app/guide/handlers/error-handling.html), so I have battle tested ways of handling failures.

## Testing

No architecture is complete without an easy way to test the functionality. My recommended strategy is to use two types of tests: unit and integration.

### Unit tests

The logic code is the easiest to test with standard unit tests. These tests will test all paths through the logic. Since there is no infrastructure setup required, these tests are easy to read and fairly straightforward. I'll write a test for the most complicated example as that will cover most special cases that I could need.

Instantiating the system under test is nothing more than creating a new instance of the class. Here we see the first testing benefit: a lack of injectables into the handler lets me create an instance very easily. In my actual test class I put this in a private readonly field that gets reused throughout my tests.

All injections happen in the `Handle` function. Keep injected data simple and specific to the case under test. I create a default `_walk` that I pass to the function or that can serve as an [object mother](https://martinfowler.com/bliki/ObjectMother.html) that will be modified to the needs of the test. The `_otherWalk` is similar to the `_walk` object, but with another dog that crossed paths with ours. The `Func<byte[]>` is now an easy-to-stub method with a small closure to capture whether the function has been called. I can also easily change the date should I need to.

The main reason I passed the date along is to showcase how everything that can vary, should be kept out of the logic code. Putting that `DateTimeOffset.Now` into my logic would make it a lot harder to test.

After executing the logic, I don't need to worry about capturing decisions, checking that a filesystem write has been executed or messages placed on a bus as that is literally communicated back to me. All I need to do is check that the right decisions come out of the scenario at hand.

```csharp
[Test]
public async Task When_other_dog_encountered_then_do_indicate_dog_encountered()
{
    var getPictureCalled = false;
    var (result, outgoingMessages, entityFrameworkInsert) = new MetFriendsHandler().Handle(
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
```

As far as test setup goes, I have no clear winner for a test framework. [xUnit.NET](https://xunit.net), [NUnit](https://nunit.org) and the newcomer [TUnit](https://tunit.dev) are all viable frameworks to test with. Personally I like xUnit for their terminology with Fact and Theory, but the newcomer TUnit is quickly capturing my interest. I've used it here to encourage everybody to keep experimenting and learning.

I have the similar thoughts on assertion and mocking libraries. There are no bad choices here. They are tools to get a job done, pick the one right for the job.

### Integration tests

Now that I've tackled the easy part, let's look at the complex part. Infrastructure code is not easy to test, no matter how you twist or turn it. I've tried mocking it out, I've tried using the Entity Framework in-memory database, I've tried sacrificing managers to the god [Maniae](https://en.wikipedia.org/wiki/Maniae). This is where integration tests come into play.

To run the web server in-memory instead of a real webserver such as Kestrel or IIS, I'll either use the [WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) directly as this is fairly easy. This is what I did in my basic (and honestly, quite useless) integration test. In reality, I'll use a library such as [Alba](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests).

[Don't try to simulate the database](https://learn.microsoft.com/en-us/ef/core/providers/in-memory/?tabs=dotnet-core-cli). I create a local test database, and I spin up a container or lightweight database in my CI/CD pipeline. [Test containers](https://testcontainers.com) can be quite convenient for those. I run my migration scripts, then I test against that database. This way, I ensure that my migration scripts work and that the statements that are executed on the database are correct as well. Double win. To reset a database to a known good point, I use [Respawn](https://github.com/jbogard/Respawn). I prefer resetting the database before each testrun. This ensures that the database before each test is empty, so I can add the state that I need. After a test fails, I have access to the data to debug efficiently.

Write to the local file system when integration testing. In a CI/CD environment, my tests run in a container that gets disposed of afterwards, so I'm not afraid to try to do write operations to test if those work. I can even publish the test output as an artefact if I want to inspect it afterwards.

When I'm working with external systems, I do mock/fake/stub those. The twist, I call each system with test data, and I record their responses. I prefer doing this with a test system, but I will use the real API if I have no other option. In the last case, I'll never do that unannounced. I'll get in touch with their team and coordinate a moment and specify which data I'll be sending across. In all cases I keep the data that I sent and the responses. Both good and bad responses are used in my integration tests, so my system is prepared for all possible scenarios.

In practice, I'll try to mock/fake/stub the return of the `HttpClient`. [WireMock.NET](https://github.com/WireMock-Net/WireMock.Net) can come in quite handy in these scenarios. If I use libraries such as [Refit](https://reactiveui.github.io/refit/), [RestSharp](https://restsharp.dev) or [Flurl](https://flurl.dev), I'll use their built-in test support.

```csharp
[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public class MetFriendsIntegrationTests(WebAppFactory webAppFactory)
{
    [Test]
    public async Task Get_response_bad_request()
    {
        var client = webAppFactory.CreateClient();

        using var response = await client.GetAsync("/friends/1");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}

public class WebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // let Oakton accept --environment variables
        OaktonEnvironment.AutoStartHost = true;

        // disable all external setup so the integration tests don't start sending out messages
        builder.ConfigureTestServices(services => services.DisableAllExternalWolverineTransports());
    }

    public Task InitializeAsync()
    {
        // Grab a reference to the server
        // This forces it to initialise.
        // By doing it within this method, it's thread safe.
        // And avoids multiple initialisations from different tests if parallelisation is switched on
        _ = Server;
        return Task.CompletedTask;
    }
}
```

## Sources

[A-Frame Architecture with Wolverine](https://jeremydmiller.com/2023/07/19/a-frame-architecture-with-wolverine/)
[James Shore A-Frame Architecture](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks#a-frame-arch)
[Wolverine Docs](https://wolverine.netlify.app/tutorials/)
[Alba integration test framework](https://jasperfx.github.io/alba/)
[Integration testing in dotnet](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
[TUnit](https://tunit.dev)