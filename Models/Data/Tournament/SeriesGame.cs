public partial class SeriesGame
{
    public SeriesGame()
    {
    }

    public long ID { get; set; }
    public long SeriesID { get; set; }
    public long GameNumber { get; set; }
    public long? GameID { get; set; }

    public virtual RoundSeries? Series { get; set; }
    public virtual Game? Game { get; set; }
}