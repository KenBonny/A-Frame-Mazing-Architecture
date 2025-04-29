using MoreThanCode.AFrameExample.Database;
using Wolverine;

namespace MoreThanCode.AFrameExample.Shared;

public class EntityFrameworkInsert<T>(T entity) : ISideEffect where T : class
{
    public async Task ExecuteAsync(DogWalkingContext db)
    {
        db.Attach(entity);
        await db.SaveChangesAsync();
    }
}