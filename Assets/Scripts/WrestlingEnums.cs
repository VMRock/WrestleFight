using System;

namespace WrestleGame
{
    public enum FighterState
    {
        Idle,
        Moving,
        Dashing,
        LightAttack,
        HeavyAttack,
        Grappling,
        BeingGrappled,
        RopeBouncing,
        Blocking,
        HitStun,
        KnockedDown,
        GettingUp,
        Pinning,
        BeingPinned,
        SpecialFinisher,
        BeingFinished,
        Taunting,
        Victory,
        Defeat
    }

    public enum AttackType
    {
        LightStrike1,
        LightStrike2,
        LightStrike3,
        HeavyStrike,
        BodySlam,
        IrishWhip,
        SpecialFinisher,
        GroundStomp,
        RopeReboundAttack
    }

    public enum ControlType
    {
        Player1,
        Player2,
        AI
    }

    public enum AIDifficulty
    {
        Easy,
        Normal,
        Hard,
        Champion
    }

    public enum GameState
    {
        MainMenu,
        MatchIntro,
        Playing,
        Paused,
        PinningCount,
        MatchOver
    }

    public enum GameMode
    {
        PlayerVsCpu,
        PlayerVsPlayer,
        SpectatorCpuVsCpu
    }

    public enum CameraViewMode
    {
        DynamicAction,    // Broadcast smart camera tracking both fighters
        ThirdPersonP1,    // Follow P1 over the shoulder
        RingsideDramatic, // Dramatic low angle ringside view
        OrbitFree,        // Free mouse/stick orbit cam
        TopDownArena      // Tactical overhead view
    }
}
