# A-Frame-Mazing Architecture

## Table of Content

1. What is A-Frame Architecture?

## What is A-Frame Architecture?

A-frame architecture is pretty simple: it separates interfacing with infrastructure from taking decisions using logic; between the two is a controller who orchestrates the flow of data and information. Everything in your code should resprect that separation.

![A-Frame Architecture triangle.png](A-Frame%20triangle.png "A-Frame Architecture triangle")

The idea behind this separation is that each component is easy to reason about, does not influence the other and can thus be easily changed, tested and replaced. Lets look at each in more detail.

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






## Sources

[A-Frame Architecture with Wolverine](https://jeremydmiller.com/2023/07/19/a-frame-architecture-with-wolverine/)
[James Shore A-Frame Architecture](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks#a-frame-arch)