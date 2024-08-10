
using Microsoft.EntityFrameworkCore;

public partial class LeagueSitesContext : DbContext
{
    public LeagueSitesContext()
    {
    }

    public LeagueSitesContext(DbContextOptions<LeagueSitesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BattingEvent> BattingEvents { get; set; }
    public virtual DbSet<BattingLineupEntry> BattingLineupEntries { get; set; }
    public virtual DbSet<BracketRound> BracketRounds { get; set; }
    public virtual DbSet<Event> Events { get; set; }
    public virtual DbSet<Game> Games { get; set; }
    public virtual DbSet<GameStatus> GameStatuses { get; set; }
    public virtual DbSet<Invitation> Invitations { get; set; }
    public virtual DbSet<InvitationEmail> InvitationEmails { get; set; }
    public virtual DbSet<InvitationRole> InvitationRoles { get; set; }
    public virtual DbSet<InvitationStatus> InvitationStatuses { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<News> News { get; set; }
    public virtual DbSet<Player> Players { get; set; }
    public virtual DbSet<PlayerBio> PlayerBios { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<RoundSeries> RoundSeries { get; set; }
    public virtual DbSet<RoundRobinGame> RoundRobinGames { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeriesGame> SeriesGames { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<Tournament> Tournaments { get; set; }
    public virtual DbSet<TournamentBracket> TournamentBrackets { get; set; }
    public virtual DbSet<TournamentRoundRobin> TournamentRoundRobins { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserLogin> UserLogins { get; set; }
    public virtual DbSet<UserLoginSource> UserLoginSources { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            throw new Exception("Must configure database context in startup");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<BattingEvent>(entity =>
        {
            entity.ToTable("BattingEvent");

            entity.HasKey(c => new { c.GameID, c.IsHostTeam, c.Index });

            entity.HasOne(d => d.Game)
                .WithMany(p => p.BattingEvents)
                .HasForeignKey(d => d.GameID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Player)
                .WithMany(p => p.BattingEvents)
                .HasForeignKey(d => d.PlayerID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<BattingLineupEntry>(entity =>
        {
            entity.ToTable("BattingLineupEntry");

            entity.HasOne(d => d.Game)
                .WithMany(p => p.BattingLineupEntries)
                .HasForeignKey(d => d.GameID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Player)
                .WithMany(p => p.BattingLineupEntries)
                .HasForeignKey(d => d.PlayerID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Type)
                .HasConversion(
                    v => v.ToString(),
                    v => (EventType)Enum.Parse(typeof(EventType), v));

            entity.Property(e => e.Date).IsRequired();

            entity.HasOne(d => d.User)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("Game");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Date).IsRequired();

            entity.HasOne(d => d.HostTeam)
                .WithMany(p => p.GameHostTeam)
                .HasForeignKey(d => d.HostTeamID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Location)
                .WithMany(p => p.Games)
                .HasForeignKey(d => d.LocationID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Season)
                .WithMany(p => p.Games)
                .HasForeignKey(d => d.SeasonID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Status)
                .WithMany(p => p.Games)
                .HasForeignKey(d => d.StatusID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.VisitingTeam)
                .WithMany(p => p.GameVisitingTeam)
                .HasForeignKey(d => d.VisitingTeamID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<GameStatus>(entity =>
        {
            entity.ToTable("GameStatus");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("Invitation");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(d => d.Player)
                .WithMany(p => p.Invitations)
                .IsRequired(false)
                .HasForeignKey(d => d.PlayerID);

            entity.HasOne(d => d.Status)
                .WithMany(p => p.Invitations)
                .HasForeignKey(d => d.StatusID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Team)
                .WithMany(p => p.Invitations)
                .HasForeignKey(d => d.TeamID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User)
                .WithMany(p => p.Invitations)
                .IsRequired(false)
                .HasForeignKey(d => d.UserID);
        });

        modelBuilder.Entity<InvitationEmail>(entity =>
        {
            entity.ToTable("InvitationEmail");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Email)
                .IsRequired();

            entity.HasOne(d => d.Invitation)
                .WithMany(p => p.InvitationEmails)
                .HasForeignKey(d => d.InvitationID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvitationRole>(entity =>
        {
            entity.ToTable("InvitationRole");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(d => d.Invitation)
                .WithMany(p => p.InvitationRoles)
                .HasForeignKey(d => d.InvitationID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Role)
                .WithMany(p => p.InvitationRoles)
                .HasForeignKey(d => d.RoleID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvitationStatus>(entity =>
        {
            entity.ToTable("InvitationStatus");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("Location");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.City).IsRequired();

            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Contents).IsRequired();

            entity.Property(e => e.Date).IsRequired();

            entity.Property(e => e.Source).IsRequired();

            entity.Property(e => e.Title).IsRequired();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("Player", t =>
            {
                t.HasCheckConstraint("ShortCode", "length(ShortCode)<=9");
            });

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasIndex(e => e.ShortCode)
                .IsUnique();
        });

        modelBuilder.Entity<PlayerBio>(entity =>
        {
            entity.ToTable("PlayerBio");

            entity.HasIndex(e => e.PlayerID)
                .IsUnique();

            entity.HasKey(d => d.PlayerID);

            entity.HasOne(pb => pb.Player)
                .WithOne(p => p.Bio)
                .HasForeignKey<PlayerBio>(p => p.PlayerID);
        });

        modelBuilder.Entity<PlayerSocial>(entity =>
        {
            entity.ToTable("PlayerSocial");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.PlayerID).IsRequired();

            entity.Property(e => e.SocialPlatformID).IsRequired();

            entity.Property(e => e.Account).IsRequired();

            entity.HasOne(e => e.Player)
                .WithMany(t => t.Socials)
                .HasForeignKey(e => e.PlayerID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Platform)
                .WithMany(t => t.PlayerSocials)
                .HasForeignKey(e => e.SocialPlatformID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired();
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.ToTable("Season");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.StartDate).IsRequired();

            entity.Property(e => e.Subseason).IsRequired();
        });

        modelBuilder.Entity<SocialPlatform>(entity =>
        {
            entity.ToTable("SocialPlatform");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name).IsRequired();

            entity.Property(e => e.BaseUrl)
                .HasConversion(v => v != null ? v.ToString() : string.Empty, v => new Uri(v))
                .IsRequired();
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Team");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Abbreviation).IsRequired();

            entity.Property(e => e.BackgroundColor)
                .IsRequired()
                .HasDefaultValueSql("'255, 255, 255'");

            entity.Property(e => e.Color)
                .IsRequired()
                .HasDefaultValueSql("'0, 0, 0'");

            entity.Property(e => e.Location).IsRequired();

            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<TeamSocial>(entity =>
        {
            entity.ToTable("TeamSocial");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.TeamID).IsRequired();

            entity.Property(e => e.SocialPlatformID).IsRequired();

            entity.Property(e => e.Account).IsRequired();

            entity.HasOne(e => e.Team)
                .WithMany(t => t.Socials)
                .HasForeignKey(e => e.TeamID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(e => e.Platform)
                .WithMany(t => t.TeamSocials)
                .HasForeignKey(e => e.SocialPlatformID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("Tournament");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Season)
                .WithMany(t => t.Tournaments)
                .HasForeignKey(e => e.SeasonID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TournamentBracket>(entity =>
        {
            entity.ToTable("TournamentBracket");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Tournament)
                .WithMany(t => t.Brackets)
                .HasForeignKey(e => e.TournamentID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TournamentRoundRobin>(entity =>
        {
            entity.ToTable("TournamentRoundRobin");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Tournament)
                .WithMany(t => t.RoundRobins)
                .HasForeignKey(e => e.TournamentID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<BracketRound>(entity =>
        {
            entity.ToTable("BracketRound");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Bracket)
                .WithMany(t => t.Rounds)
                .HasForeignKey(e => e.BracketID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RoundRobinGame>(entity =>
        {
            entity.ToTable("RoundRobinGame");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.RoundRobin)
                .WithMany(t => t.Games)
                .HasForeignKey(e => e.TournamentRoundRobinID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RoundSeries>(entity =>
        {
            entity.ToTable("RoundSeries");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Round)
                .WithMany(t => t.Series)
                .HasForeignKey(e => e.RoundID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SeriesGame>(entity =>
        {
            entity.ToTable("SeriesGame");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Series)
                .WithMany(t => t.Games)
                .HasForeignKey(e => e.SeriesID)
                .OnDelete(DeleteBehavior.ClientSetNull);

        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<UserLogin>(entity =>
        {
            entity.ToTable("UserLogin");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name).IsRequired();

            entity.Property(e => e.Email).IsRequired();

            entity.Property(e => e.IsPrimary).HasDefaultValueSql("1");

            entity.HasOne(d => d.LoginSource)
                .WithMany(p => p.UserLogins)
                .HasForeignKey(d => d.LoginSourceID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserLogins)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<UserLoginSource>(entity =>
        {
            entity.ToTable("UserLoginSource");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Source).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRole");

            entity.HasIndex(e => e.ID)
                .IsUnique();

            entity.Property(e => e.ID)
                .ValueGeneratedOnAdd();

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Role)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleID)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}