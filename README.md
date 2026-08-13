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
| GET | `/api/Site/Config` | Site configuration: name, about, executives, socials, links |
| GET | `/api/User` | Current user auth status (name, claims) |
| GET | `/api/User/Permissions/{id}` | Current user's permissions for a team |
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
| POST | `/api/Executive/Season/Playoffs` | Executive, Webmaster | Create the current year's playoffs season and an empty tournament |

### Tournament Management

All Executive or Webmaster. These endpoints speak in structured shapes; see
[Playoff and tournament structure](#playoff-and-tournament-structure) below.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Tournament/{id}` | Full structure with every seed, matchup and result resolved, plus reference data for the management UI |
| GET | `/api/Tournament/{id}/DefaultSeeding` | The seeding rule to offer by default (whole regular season standings) |
| POST | `/api/Tournament` | Create a tournament for a season |
| POST | `/api/Tournament/{id}/Bracket` | Add a bracket, including its rounds and series |
| PUT | `/api/TournamentBracket/{id}` | Replace a bracket's structure, keeping series that have games |
| DELETE | `/api/TournamentBracket/{id}` | Delete a bracket (refused if any game is scheduled) |
| POST | `/api/Tournament/{id}/RoundRobin` | Add a round robin pool |
| PUT | `/api/TournamentRoundRobin/{id}` | Update a pool's name, seeding and History flag |
| DELETE | `/api/TournamentRoundRobin/{id}` | Delete a pool (refused if any game is scheduled) |
| POST | `/api/Series/{id}/Game` | Schedule one game of a series. Host and visitor are derived from the series' host order and resolved spots, so only a date and park are supplied |
| DELETE | `/api/Series/Game/{seriesGameID}` | Take a game out of a series and move it to the deleted bin |
| POST | `/api/TournamentRoundRobin/{id}/Game` | Add a game to a pool (teams chosen explicitly) |
| DELETE | `/api/TournamentRoundRobin/Game/{roundRobinGameID}` | Take a game out of a pool and move it to the deleted bin |


## Playoff and tournament structure

A playoffs run is a `Season` row with `Subseason = "Playoffs"`, holding a `Tournament`
that owns any number of `TournamentBracket` and `TournamentRoundRobin` rows. Playoff
games are ordinary `Game` rows in the playoffs season, linked into the structure by
`SeriesGame` and `RoundRobinGame`. Scores are entered on the normal game pages, and the
bracket resolves itself from those results.

Three columns hold compact encodings. `TournamentFormats` is the only place that reads
or writes them, and `Models/DTOs/TournamentDtos.cs` exposes structured equivalents so no
caller has to build these strings by hand.

### `RoundSeries.Matchup`

Two dash-separated spots, each a source character followed by a number.

| Encoding | Meaning |
|----------|---------|
| `#4` | The team seeded 4th |
| `w5` | The winner of series number 5 |
| `l5` | The loser of series number 5 |
| `r2` | The 2nd best team still alive, by initial seed (re-seeding brackets) |

So `#1-#8` is top seed against bottom seed, `w5-w6` is a fixed bracket final, and
`r1-r4` re-pairs whoever is left. Series numbers are unique within a bracket because
later rounds refer to them.

### `RoundSeries.HostOrder`

One digit per game, naming which of the two spots hosts it. `121` is a three game series
where spot 1 (the higher seed) hosts games 1 and 3. The length of the string is the
length of the series, and a `"Best of"` series is won by taking a majority of it.

### `TournamentBracket.SeedingConfiguration` and `TournamentRoundRobin.SeedingConfiguration`

Semicolon-separated rules of `outputStart-outputEnd,Result,SourceType:SourceID:rankStart-rankEnd`.

- `Result` is `Standings` or `Losers`
- `SourceType` is `Season`, `BracketRound` or `TournamentRoundRobin` (`Losers` only works with `BracketRound`)

`1-8,Standings,Season:13:1-8` seeds a bracket from the top eight of season 13.
`1-1,Losers,BracketRound:15:1-1;2-2,Standings,BracketRound:18:1-1` fills a consolation
pool with the best team knocked out in round 15 plus the winner of round 18.

A rule whose source is only partly decided fills the seeds it can and leaves the rest
empty, so a consolation pool can be set up before the games feeding it have been played.
An empty or null value means "seed from this year's regular season standings", which the
tournament resolves at read time. New structures always write the rule out explicitly,
because ranking the losers of a round requires knowing the original seed order.

### Known limitations

- Rounds are ordered for display by descending series count, so each round must be
  smaller than the one before it. `TournamentStructureValidator` enforces this. Lifting
  it needs an explicit round order column.
- The guided builder lays out 2, 4, 8 and 16 team brackets. Byes are not supported.
- `RoundSeries.Format` is `"Best of"` in practice; `"Aggregate"` is partly implemented.


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
