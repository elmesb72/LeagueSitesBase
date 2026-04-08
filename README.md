## About

This package is intended to be used as a template for different sports league websites. It uses ASP.NET Core with Entity Framework Core and SQLite.

The application serves both Razor Pages (legacy frontend) and API controllers. The API layer is designed to support a Svelte frontend replacement.

League-specific settings are specified in appsettings.json.


## API Endpoints

### Public

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/SiteStatus` | Health check |
| HEAD | `/api/SiteStatus` | Health check (no body) |
| GET | `/api/Home` | Homepage data: upcoming games, news, standings |
| GET | `/api/Teams` | All active teams |
| GET | `/api/Teams/{id}` | Single team by ID |
| GET | `/api/Teams/{id}/Players` | Active players on a team (jersey number → name) |
| GET | `/api/Schedule?year=` | Games for a year, grouped by location |
| GET | `/api/Scores?day=` | Games and standings for a specific date |
| GET | `/api/Standings?year=` | Full standings with streaks for a season |
| GET | `/api/Locations` | All active locations with game counts |
| GET | `/api/Player/{code}` | Player profile by short code |
| GET | `/api/Scorecard/{id}` | Batting events and lineup for a game |
| GET | `/api/History` | League history: champions and best records by year |
| GET | `/api/Playoffs?year=` | Playoff brackets, series, round robins |
| GET | `/api/User/Permissions/{id}` | Current user's permissions for a team |

### Authenticated

| Method | Endpoint | Permissions | Description |
|--------|----------|-------------|-------------|
| GET | `/api/User/Profile` | Authenticated | Current user's profile and invitations |
| DELETE | `/api/UserLogin/Delete/{id}` | Authenticated | Delete a non-primary login |
| POST | `/api/UserLogin/Favourite/{id}` | Authenticated | Set a login as primary |

### Game Management

| Method | Endpoint | Permissions | Description |
|--------|----------|-------------|-------------|
| GET | `/api/Game/{id}` | Public (includes `canEdit` flag) | Game details with edit permission check |
| POST | `/api/Game` | Manager, Scorer, Executive, Webmaster | Create a game |
| PUT | `/api/Game/{id}` | Team-scoped: Manager/Scorer on game's teams, or Executive/Webmaster | Update a game |

### News

| Method | Endpoint | Permissions | Description |
|--------|----------|-------------|-------------|
| GET | `/api/News/{id}` | Reporter, Scorer, Manager, Executive, Webmaster | Get a news post (includes `canEdit` flag) |
| POST | `/api/News` | Reporter, Scorer, Manager, Executive, Webmaster | Create a news post |
| PUT | `/api/News/{id}` | Author, or Executive/Webmaster | Update a news post |
| DELETE | `/api/News/{id}` | Author, or Executive/Webmaster | Soft-delete a news post |
| GET | `/api/News/RecycleBin` | Authenticated | Deleted posts by the current user |

### Invitations

| Method | Endpoint | Permissions | Description |
|--------|----------|-------------|-------------|
| GET | `/api/Invitation/{id}` | Manager, Scorer, Reporter, Executive, Webmaster | Get an invitation |
| POST | `/api/Invitation` | Manager, Scorer, Reporter, Executive, Webmaster | Create an invitation |
| PUT | `/api/Invitation/{id}` | Manager, Scorer, Reporter, Executive, Webmaster | Update an invitation |

### Executive (Admin)

| Method | Endpoint | Permissions | Description |
|--------|----------|-------------|-------------|
| GET | `/api/Executive/Dashboard` | Executive, Webmaster | Teams, locations, current season/playoffs |
| POST | `/api/Executive/Season` | Executive, Webmaster | Create a new regular season for the current year |
| PATCH | `/api/Executive/Status/{entity}/{id}` | Executive, Webmaster | Toggle active status of a team or park |
| GET | `/api/Executive/DeletedGames` | Executive, Webmaster | Games with "Deleted" status |


## Authorization Model

Permissions are two-tiered:

- **Site-level roles** (via `UserRoles`): Webmaster, Executive
- **Team-level roles** (via `InvitationRoles`): Manager, Scorer, Reporter

The authorization framework uses ASP.NET's `IAuthorizationHandler` with two requirement types:

- `PermissionsScopeRequirement` — checks if the user has any of the specified roles at the site or team level. Used via `[Authorize(Policy = "Scope:Role1,Role2")]` with a dynamic `ScopePolicyProvider`.
- `TeamScopedRequirement` — checks roles against a specific resource's teams. The resource implements `ITeamScoped` to provide its related teams. Used via `IAuthorizationService.AuthorizeAsync(User, resource, requirement)`.


## Local environment setup

- Install VSCode
- Install the following extensions:
  - EditorConfig

- Save a local copy of the league database, and update the path in appsettings.Development.json.


## Implementations

### Site settings

- appsettings.json
  - Port numbers [for hosting]
  - ConnectionString [for DB]
  - Authentication providers [for OAuth login]
  - League name [for titles]
  - (Optional) League short name/abbreviation
  - Home page:
    - News config
      - Max age (days)
      - Min items
    - About Blurb
    - League executives
    - League socials
    - League links (e.g. sister leagues)
    - Useful files (e.g. rules, scoresheets, waiver form, etc.)
  - TBD:
    - Theme
    - Scoring
      - Sport [for stats systems]
      - Point systems
      - Tiebreakers

### Static content
- League logo
- Team logos
- Files (e.g. printable scoresheets, league rules, waiver forms)

A first user should be invited as a webmaster, who can set up teams and locations, and invite owners for each team to register.
