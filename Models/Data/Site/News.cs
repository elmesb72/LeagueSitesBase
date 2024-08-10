public partial class News
{
    public long ID { get; set; }
    public required DateTime Date { get; set; }
    public required string Title { get; set; }
    public required string Contents { get; set; }
    public required string Source { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }
}