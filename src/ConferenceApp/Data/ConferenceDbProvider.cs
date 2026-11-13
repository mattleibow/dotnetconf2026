namespace ConferenceApp.Data;

public class ConferenceDbProvider
{
    private readonly Lazy<Task<ConferenceDatabase>> database = new(ConferenceDatabase.LoadAsync);

    public Task<ConferenceDatabase> GetDatabaseAsync() => database.Value;
}
