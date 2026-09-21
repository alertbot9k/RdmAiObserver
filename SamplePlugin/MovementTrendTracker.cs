using System;
using System.Numerics;

namespace SamplePlugin;

public sealed record MovementTrend(
    float WindowSeconds,
    float PlayerSpeed,
    float CrystalSpeed,
    float CrystalDistanceChangePerSecond,
    bool PlayerMovingTowardCrystal,
    bool CrystalMoving);

public sealed class MovementTrendTracker
{
    private DateTime? previousAtUtc;
    private Vector3? previousPlayer;
    private Vector3? previousCrystal;
    private float? previousCrystalDistance;

    public MovementTrend? Update(GameState state, DateTime capturedAtUtc)
    {
        var player = state.Player;
        var objective = state.Objective;
        var currentPlayer = player == null ? (Vector3?)null : new(player.X, player.Y, player.Z);
        var currentCrystal = objective == null ? (Vector3?)null : new(objective.X, objective.Y, objective.Z);
        MovementTrend? result = null;

        if (previousAtUtc.HasValue && capturedAtUtc > previousAtUtc.Value)
        {
            var seconds = (float)(capturedAtUtc - previousAtUtc.Value).TotalSeconds;
            var playerSpeed = currentPlayer.HasValue && previousPlayer.HasValue
                ? Vector3.Distance(currentPlayer.Value, previousPlayer.Value) / seconds : 0f;
            var crystalSpeed = currentCrystal.HasValue && previousCrystal.HasValue
                ? Vector3.Distance(currentCrystal.Value, previousCrystal.Value) / seconds : 0f;
            var distanceRate = objective != null && previousCrystalDistance.HasValue
                ? (objective.DistanceToPlayer - previousCrystalDistance.Value) / seconds : 0f;
            result = new(seconds, playerSpeed, crystalSpeed, distanceRate,
                distanceRate < -0.25f, crystalSpeed > 0.25f);
        }

        previousAtUtc = capturedAtUtc;
        previousPlayer = currentPlayer;
        previousCrystal = currentCrystal;
        previousCrystalDistance = objective?.DistanceToPlayer;
        return result;
    }

    public void Reset()
    {
        previousAtUtc = null;
        previousPlayer = null;
        previousCrystal = null;
        previousCrystalDistance = null;
    }
}
