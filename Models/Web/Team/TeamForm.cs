public class TeamForm
{
    public string? Location { get; set; }
    public string? Name { get; set; }
    public string? Abbreviation { get; set; }
    public string? BackgroundColor { get; set; }
    public string? Color { get; set; }

    public TeamForm() { }

    public TeamForm(Team t)
    {
        Location = t.Location;
        Name = t.Name;
        Abbreviation = t.Abbreviation;
        BackgroundColor = t.BackgroundColor;
        Color = t.Color;
    }

    public void UpdateExistingTeam(ref Team t)
    {
        if (Location == null || Name == null || Abbreviation == null || BackgroundColor == null || Color == null)
        {
            return;
        }
        t.Location = Location;
        t.Name = Name;
        t.Abbreviation = Abbreviation;
        t.BackgroundColor = BackgroundColor;
        t.Color = Color;
    }
}