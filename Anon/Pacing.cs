using System;
using Dalamud.Game.ClientState.Conditions;

namespace Anon;

public sealed class Pacing(Configuration config)
{
    private const int BurstMilliseconds = 5000;
    private const int BurstMultiplier = 4;

    private bool burstPending;
    private long burstEnds;
    private bool paused;
    private int spent;

    public bool CanRedraw => !paused && spent < Budget * (IsBursting ? BurstMultiplier : 1);

    private int Budget => Math.Max(1, config.Safety.MaxRedrawsPerFrame);

    public static bool IsBetweenAreas() => Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51];

    public void StartBurst() => burstPending = true;

    public void Begin()
    {
        if (burstPending)
        {
            burstPending = false;
            burstEnds = Environment.TickCount64 + BurstMilliseconds;
        }

        paused = IsPaused();
        spent = 0;
    }

    public void Spend() => spent++;

    private bool IsBursting => Environment.TickCount64 < burstEnds;

    private bool IsPaused()
    {
        var condition = Plugin.Condition;
        var safety = config.Safety;
        return condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Plugin.ClientState.IsGPosing
            || (safety.PauseInDuty && condition[ConditionFlag.BoundByDuty])
            || (safety.PauseInCombat && condition[ConditionFlag.InCombat]);
    }
}
