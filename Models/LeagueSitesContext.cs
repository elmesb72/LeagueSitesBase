
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
}