public class TeamResult
    {
        public readonly Game Game;
        public readonly GameResult Result;
        public bool WasHome;
        public readonly int RunsScored;
        public readonly int RunsAllowed;
        public int RunDifferential
        {
            get
            {
                return RunsScored - RunsAllowed;
            }
        }

        public TeamResult(Game g, Team t)
        {
            Game = g;

            if (g.HostTeam == t)
            {
                WasHome = true;
                if (g.ScoreHost > g.ScoreVisitor)
                {
                    Result = GameResult.Win;
                }
                else if (g.ScoreHost == g.ScoreVisitor)
                {
                    Result = GameResult.Tie;
                }
                else if (g.ScoreHost < g.ScoreVisitor)
                {
                    Result = GameResult.Loss;
                }
                RunsScored += g.ScoreHost is not null ? (int)g.ScoreHost : 0;
                RunsAllowed += g.ScoreVisitor is not null ? (int)g.ScoreVisitor : 0;
            }
            else if (g.VisitingTeam == t)
            {
                WasHome = false;
                if (g.ScoreHost > g.ScoreVisitor)
                {
                    Result = GameResult.Loss;
                }
                else if (g.ScoreHost == g.ScoreVisitor)
                {
                    Result = GameResult.Tie;
                }
                else if (g.ScoreHost < g.ScoreVisitor)
                {
                    Result = GameResult.Win;
                }
                RunsScored += g.ScoreVisitor is not null ? (int)g.ScoreVisitor : 0;
                RunsAllowed += g.ScoreHost is not null ? (int)g.ScoreHost : 0;
            }
        }

    }