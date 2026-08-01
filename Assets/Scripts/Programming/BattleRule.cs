public sealed class BattleRule
{
    public ConditionType Condition { get; }
    public HeroActionType Action { get; }
    public string Source { get; }

    public BattleRule(
        ConditionType condition,
        HeroActionType action,
        string source)
    {
        Condition = condition;
        Action = action;
        Source = source;
    }
}