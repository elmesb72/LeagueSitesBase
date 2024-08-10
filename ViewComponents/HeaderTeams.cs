using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class HeaderTeamsViewComponent : ViewComponent
{
    readonly LeagueSitesContext dbContext;
    public HeaderTeamsViewComponent(LeagueSitesContext context)
    {
        dbContext = context;
    }

    public async Task<IViewComponentResult> InvokeAsync() => View((await dbContext.Teams.Where(t => t.Active).ToListAsync()).OrderBy(t => t.FullName));
}
