## About

This package is intended to be used as a template for different sports league websites. It uses ASP.NET Core Razor Pages with Entity Framework Core and SQLite.

League-specific settings are specified in appsettings.json.


## Local environment setup:

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
    - Scoring
      - Sport [for stats systems]
      - Point systems
      - Tiebreakers

### Static content
- League logo
- Team logos
- Files (e.g. printable scoresheets, league rules, waiver forms)

A first user should be invited as a webmaster, who can set up teams and locations, and invite owners for each team to register.
