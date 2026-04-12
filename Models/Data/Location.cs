public partial class Location : IEquatable<Location>
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

        public bool Equals(Location? other)
        {
            return ID == other?.ID;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Location);
        }

        public override int GetHashCode()
        {
            return Convert.ToInt32(ID);
        }

        public static bool operator ==(Location? left, Location? right)
        {
            if (left is null)
            {
                return right is null;
            }
            return left.Equals(right);
        }

        public static bool operator !=(Location? left, Location? right)
        {
            return !(left == right);
        }
    }