public partial class Location
    {
        public Location()
        {
            Games = [];
        }

        public long ID { get; set; }
        public bool Active { get; set; }
        public required string Name { get; set; }
        public string? FormalName { get; set; }
        public required string City { get; set; }
        public string? Address { get; set; }
        public string? MapsPlaceID { get; set; }

        public virtual ICollection<Game> Games { get; set; }
    }