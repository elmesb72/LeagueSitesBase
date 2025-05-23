public class RosterPartialModel
{
    public ICollection<Invitation> Invitations { get; set; } = [];
    public List<String> Permissions { get; set; } = [];
}