using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class HeaderTeamsViewComponent(LeagueSitesContext context) : ViewComponent
{
    readonly LeagueSitesContext dbContext = context;

    public async Task<IViewComponentResult> InvokeAsync() => View((await dbContext.Teams.Where(t => t.Active).ToListAsync()).OrderBy(t => t.FullName));
}
