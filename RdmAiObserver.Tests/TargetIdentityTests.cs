using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class TargetIdentityTests
{
    [Fact]
    public void Guarded_target_with_same_named_alternative_is_distinguished_by_object_id()
    {
        var state = new GameState
        {
            TerritoryId = 1293,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 },
            Target = new TargetSnapshot
            {
                ObjectId = 100, Name = "Twin", Distance = 12f, Hp = 50000, MaxHp = 58500,
                Statuses = new List<StatusSnapshot> { new() { Name = "Guard" } }
            }
        };
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
        {
            ObjectId = 200, Name = "Twin", Distance = 12f, Hp = 10000, MaxHp = 58500
        });

        var recommendation = DecisionEngine.Evaluate(state);

        Assert.Equal("Switch to Twin", recommendation.Recommendation);
    }
}
