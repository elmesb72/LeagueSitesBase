public partial class UserLogin
{
    public long ID { get; set; }
    public long UserID { get; set; }
    public long LoginSourceID { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required bool IsPrimary { get; set; }

    [JsonIgnore]
    public virtual UserLoginSource? LoginSource { get; set; }
    [JsonIgnore]
    public virtual User? User { get; set; }

}