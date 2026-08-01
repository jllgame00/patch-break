using System;

public static class RuleParser
{
    public static bool TryParse(
        string source,
        out BattleRule rule,
        out string error)
    {
        rule = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "Rule is empty.";
            return false;
        }

        string normalized = source
            .Trim()
            .ToLowerInvariant();

        string[] parts = normalized.Split(
            new[] { "=>" },
            StringSplitOptions.None
        );

        if (parts.Length != 2)
        {
            error = "Rule must contain exactly one '=>'.";
            return false;
        }

        string conditionPart = parts[0].Trim();
        string actionPart = parts[1].Trim();

        if (!conditionPart.StartsWith("if "))
        {
            error = "Rule must start with 'if'.";
            return false;
        }

        string conditionToken = conditionPart
            .Substring(3)
            .Trim();

        if (!TryParseCondition(conditionToken, out ConditionType condition))
        {
            error = $"Unknown condition: {conditionToken}";
            return false;
        }

        if (!TryParseAction(actionPart, out HeroActionType action))
        {
            error = $"Unknown action: {actionPart}";
            return false;
        }

        rule = new BattleRule(
            condition,
            action,
            source
        );

        return true;
    }

    private static bool TryParseCondition(
        string token,
        out ConditionType condition)
    {
        switch (token)
        {
            case "enemy.near":
                condition = ConditionType.EnemyNear;
                return true;

            default:
                condition = default;
                return false;
        }
    }

    private static bool TryParseAction(
        string token,
        out HeroActionType action)
    {
        switch (token)
        {
            case "slash":
                action = HeroActionType.Slash;
                return true;

            case "dash.forward":
                action = HeroActionType.DashForward;
                return true;

            case "dash.back":
                action = HeroActionType.DashBack;
                return true;

            default:
                action = HeroActionType.None;
                return false;
        }
    }
}