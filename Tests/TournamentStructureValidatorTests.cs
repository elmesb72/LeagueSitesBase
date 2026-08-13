using FluentAssertions;

namespace LeagueSitesBackend.Tests;

public class TournamentStructureValidatorTests
{
    static SeedGroupDto RegularSeasonSeeding(int teams = 8) =>
        new(1, teams, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 1, teams);

    static SeriesUpsertDto Series(long number, string spot1Type, int spot1, string spot2Type, int spot2) =>
        new(null, number, TournamentFormats.SeriesFormatBestOf, [1, 2, 1],
            new MatchupDto(new SpotRefDto(spot1Type, spot1), new SpotRefDto(spot2Type, spot2)));

    static SeriesUpsertDto Seeded(long number, int highSeed, int lowSeed) =>
        Series(number, SeriesSpotRef.Seed, highSeed, SeriesSpotRef.Seed, lowSeed);

    static SeriesUpsertDto Reseeded(long number, int rank1, int rank2) =>
        Series(number, SeriesSpotRef.Reseed, rank1, SeriesSpotRef.Reseed, rank2);

    /// <summary>The shape of a real eight team re-seeding playoff bracket.</summary>
    static BracketUpsertDto EightTeamReseedBracket() => new(
        "Main",
        TournamentFormats.BracketFormatReseed,
        Historical: true,
        [RegularSeasonSeeding()],
        [
            new RoundUpsertDto(null, "Quarter-finals",
                [Seeded(1, 1, 8), Seeded(2, 2, 7), Seeded(3, 3, 6), Seeded(4, 4, 5)]),
            new RoundUpsertDto(null, "Semi-finals", [Reseeded(5, 1, 4), Reseeded(6, 2, 3)]),
            new RoundUpsertDto(null, "Finals", [Reseeded(7, 1, 2)]),
        ]);

    [Fact]
    public void ValidateBracket_AcceptsARealEightTeamBracket()
    {
        var act = () => TournamentStructureValidator.ValidateBracket(EightTeamReseedBracket());
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateBracket_AcceptsAFixedBracketReferringToEarlierWinners()
    {
        var bracket = new BracketUpsertDto(
            "Main", TournamentFormats.BracketFormatFixed, true,
            [RegularSeasonSeeding(4)],
            [
                new RoundUpsertDto(null, "Semi-finals", [Seeded(1, 1, 4), Seeded(2, 2, 3)]),
                new RoundUpsertDto(null, "Finals",
                    [Series(3, SeriesSpotRef.Winner, 1, SeriesSpotRef.Winner, 2)]),
            ]);

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateBracket_AcceptsASingleSeriesBracket()
    {
        var bracket = new BracketUpsertDto(
            "B Seeding", TournamentFormats.BracketFormatFixed, false,
            [new SeedGroupDto(9, 10, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 9, 10)],
            [new RoundUpsertDto(null, "Finals", [Seeded(1, 9, 10)])]);

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateBracket_RequiresAName()
    {
        var bracket = EightTeamReseedBracket() with { Name = "  " };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*needs a name*");
    }

    [Fact]
    public void ValidateBracket_RequiresAtLeastOneRound()
    {
        var bracket = EightTeamReseedBracket() with { Rounds = [] };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*at least one round*");
    }

    [Fact]
    public void ValidateBracket_RequiresSeeding()
    {
        var bracket = EightTeamReseedBracket() with { Seeding = [] };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*at least one seeding rule*");
    }

    [Fact]
    public void ValidateBracket_RequiresEachRoundToHaveSeries()
    {
        var bracket = EightTeamReseedBracket() with
        {
            Rounds = [new RoundUpsertDto(null, "Quarter-finals", [])],
        };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*at least one series*");
    }

    [Fact]
    public void ValidateBracket_RejectsDuplicateSeriesNumbers()
    {
        var bracket = EightTeamReseedBracket() with
        {
            Rounds =
            [
                new RoundUpsertDto(null, "Quarter-finals",
                    [Seeded(1, 1, 8), Seeded(2, 2, 7), Seeded(3, 3, 6), Seeded(4, 4, 5)]),
                // Series 1 already exists in the round above.
                new RoundUpsertDto(null, "Semi-finals", [Reseeded(1, 1, 4), Reseeded(6, 2, 3)]),
            ],
        };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*used more than once*");
    }

    [Fact]
    public void ValidateBracket_RejectsSeedEnteringTwice()
    {
        var bracket = EightTeamReseedBracket() with
        {
            Rounds =
            [
                new RoundUpsertDto(null, "Quarter-finals",
                    // Seed 1 is in two series.
                    [Seeded(1, 1, 8), Seeded(2, 1, 7), Seeded(3, 3, 6), Seeded(4, 4, 5)]),
                new RoundUpsertDto(null, "Semi-finals", [Reseeded(5, 1, 4), Reseeded(6, 2, 3)]),
                new RoundUpsertDto(null, "Finals", [Reseeded(7, 1, 2)]),
            ],
        };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*Seed #1*only enter the bracket once*");
    }

    [Fact]
    public void ValidateBracket_RejectsReferenceToASeriesThatComesLater()
    {
        var bracket = new BracketUpsertDto(
            "Main", TournamentFormats.BracketFormatFixed, true,
            [RegularSeasonSeeding(4)],
            [
                // The first round cannot depend on the winner of a later series.
                new RoundUpsertDto(null, "Semi-finals",
                    [Series(1, SeriesSpotRef.Winner, 3, SeriesSpotRef.Seed, 4), Seeded(2, 2, 3)]),
                new RoundUpsertDto(null, "Finals",
                    [Series(3, SeriesSpotRef.Winner, 1, SeriesSpotRef.Winner, 2)]),
            ]);

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*no earlier round has a series*");
    }

    [Fact]
    public void ValidateBracket_RejectsRoundsThatDoNotShrink()
    {
        var bracket = EightTeamReseedBracket() with
        {
            Rounds =
            [
                new RoundUpsertDto(null, "Semi-finals", [Seeded(1, 1, 4), Seeded(2, 2, 3)]),
                // Same size as the round before it, so the display order would be ambiguous.
                new RoundUpsertDto(null, "Finals",
                    [
                        Series(3, SeriesSpotRef.Winner, 1, SeriesSpotRef.Winner, 2),
                        Series(4, SeriesSpotRef.Loser, 1, SeriesSpotRef.Loser, 2),
                    ]),
            ],
        };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>()
            .WithMessage("*must be smaller than the one before it*");
    }

    [Fact]
    public void ValidateBracket_RejectsAnEvenLengthBestOfSeries()
    {
        var bracket = EightTeamReseedBracket() with
        {
            Rounds =
            [
                new RoundUpsertDto(null, "Finals",
                [
                    new SeriesUpsertDto(null, 1, TournamentFormats.SeriesFormatBestOf, [1, 2],
                        new MatchupDto(new SpotRefDto(SeriesSpotRef.Seed, 1), new SpotRefDto(SeriesSpotRef.Seed, 2))),
                ]),
            ],
        };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*odd number of games*");
    }

    [Fact]
    public void ValidateBracket_RejectsUnknownBracketFormat()
    {
        var bracket = EightTeamReseedBracket() with { Format = "Ladder" };

        var act = () => TournamentStructureValidator.ValidateBracket(bracket);
        act.Should().Throw<TournamentFormatException>().WithMessage("*Unknown bracket format*");
    }

    // -------------------------------------------------------------- Round robins

    [Fact]
    public void ValidateRoundRobin_AcceptsAPoolSeededFromKnockedOutTeams()
    {
        var pool = new RoundRobinUpsertDto("A Pool", Historical: false,
        [
            new SeedGroupDto(1, 1, SeedGroup.ResultLosers, SeedGroup.SourceBracketRound, 15, 1, 1),
            new SeedGroupDto(2, 2, SeedGroup.ResultLosers, SeedGroup.SourceBracketRound, 15, 3, 3),
        ]);

        var act = () => TournamentStructureValidator.ValidateRoundRobin(pool);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateRoundRobin_RequiresANameAndSeeding()
    {
        var unnamed = new RoundRobinUpsertDto("", false, [RegularSeasonSeeding()]);
        var act = () => TournamentStructureValidator.ValidateRoundRobin(unnamed);
        act.Should().Throw<TournamentFormatException>().WithMessage("*needs a name*");

        var unseeded = new RoundRobinUpsertDto("A Pool", false, []);
        var act2 = () => TournamentStructureValidator.ValidateRoundRobin(unseeded);
        act2.Should().Throw<TournamentFormatException>().WithMessage("*at least one seeding rule*");
    }
}
