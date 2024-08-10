using System.ComponentModel.DataAnnotations.Schema;

public partial class PlayerBio
{
    public long PlayerID { get; set; }
    public string? Bats { get; set; }
    public string? Throws { get; set; }
    public string? Positions { get; set; }
    public long? Height { get; set; }
    [NotMapped]
    public long? HeightImperial
    {
        get
        {
            if (Height is null) return null;
            return (long)(((long)Height) / 2.54f);
        }
    }
    public long? Weight { get; set; }
    public long? WeightImperial
    {
        get
        {
            if (Weight is null) return null;
            return (long)(((long)Weight) * 2.20462f);
        }
    }

    public DateTime Birthdate { get; set; }
    public string? From { get; set; }
    public string? ReferredBy { get; set; }

    public virtual Player? Player { get; set; }
}