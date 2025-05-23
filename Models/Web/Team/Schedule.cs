public class Schedule
{
    public List<Game> Games { get; set; } = [];
    public DateTime Start { get; set; }
    public TimeSpan ShowFor { get; set; }
    public User? CurrentUser { get; set; }
    public List<string> CurrentUserPermissions { get; set; } = [];
    public Team? FocusTeam { get; set; }

}