namespace ConferenceApp.Data;

public class ConferenceDbProvider
{
    private readonly Lazy<Task<ConferenceDb>> db = new(ConferenceDb.LoadAsync);

    public Task<ConferenceDb> GetDbAsync() => db.Value;
}
