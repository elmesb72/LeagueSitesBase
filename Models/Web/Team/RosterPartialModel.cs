public class RosterPartialModel
{
    public ICollection<Invitation> Invitations { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
}