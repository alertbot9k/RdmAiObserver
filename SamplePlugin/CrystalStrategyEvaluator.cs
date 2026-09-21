namespace SamplePlugin;

public enum CrystalStrategy { Observe, Approach, Contest, Escort, Retreat, Regroup }

public sealed record CrystalStrategyRecommendation(
    CrystalStrategy Strategy,
    string Recommendation,
    string Reason,
    EvidenceConfidence Confidence);

public static class CrystalStrategyEvaluator
{
    public static CrystalStrategyRecommendation Evaluate(GameState state, MovementTrend? movement = null)
    {
        var team = TeamAwarenessEvaluator.Evaluate(state);
        var objective = state.Objective;
        var player = state.Player;
        if (PvpModeDetector.Detect(state) != ObservedPvpMode.CrystallineConflict || player == null)
            return Result(CrystalStrategy.Observe, "Observe", "CC or player evidence is incomplete.", EvidenceConfidence.Low);
        if (objective == null)
            return Result(CrystalStrategy.Regroup, "Regroup and locate the crystal", "The crystal is not currently observed; avoid committing alone.", team.Confidence);

        var hp = player.MaxHp == 0 ? 100f : player.Hp * 100f / player.MaxHp;
        if (hp <= 40f || team.NumericalAdvantage <= -2)
            return Result(CrystalStrategy.Retreat, $"Retreat {team.RetreatDirection}", $"Local advantage is {team.NumericalAdvantage:+#;-#;0} and HP is {hp:F0}%.", team.Confidence);
        if (objective.DistanceToPlayer <= 10f && objective.EnemiesWithin10Yalms > 0)
            return Result(CrystalStrategy.Contest, "Contest the crystal", $"{objective.EnemiesWithin10Yalms} enemy/enemies are on the crystal and local numbers are sustainable.", team.Confidence);
        if (objective.DistanceToPlayer <= 15f && movement?.CrystalMoving == true && team.NumericalAdvantage >= 0)
            return Result(CrystalStrategy.Escort, "Escort the moving crystal", "The crystal is moving and the nearby team is not outnumbered.", team.Confidence);
        if (objective.DistanceToPlayer > 15f && team.IsIsolated)
            return Result(CrystalStrategy.Regroup, $"Regroup {team.RetreatDirection}", "The player is separated from allies while enemies are nearby.", team.Confidence);
        if (objective.DistanceToPlayer > 10f)
            return Result(CrystalStrategy.Approach, "Approach the crystal", $"The crystal is {objective.DistanceToPlayer:F0} yalms away.", team.Confidence);
        return Result(CrystalStrategy.Escort, "Escort the crystal", "The player is near the objective without an immediate numerical disadvantage.", team.Confidence);
    }

    private static CrystalStrategyRecommendation Result(CrystalStrategy strategy, string text, string reason, EvidenceConfidence confidence) =>
        new(strategy, text, reason, confidence);
}
