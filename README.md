Local environment setup

VSCode
EditorConfig

This package is intended to fail or work in a degraded state if run on its own. This package contains the logic but no content. It's used to initialize a site, but the content must be imported into this package from a league-specific repo. The steps to achieve this are TBD. In production, it's expected that this repo would be pulled into a folder per site, merged with site-specific content, then built and deployed.

To replace:

appsettings.json
- Port numbers [for hosting]
- ConnectionString [for DB]
- League name [for titles]
- News config
  - Max age (days)
  - Min items
- About Blurb
- Executive links
- Scoring
  - Sport [for stats systems]
  - Point systems
  - Tiebreakers

appsettings.Development.json
- ConnectionString [for DB for local testing]

Static content
- League logo
- Team logos
- Files (e.g. printable scoresheets, league rules, waiver forms)

Data
- Teams
- Users
  - Site admin/League execs
  - Team managers/scorers
- Locations

