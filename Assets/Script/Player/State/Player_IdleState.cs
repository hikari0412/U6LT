using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;

public class Player_IdleState : PlayerStateBase
{
    private Character ecmCharacter;   // 角色控制器（ECM2）
    private InputControls input;      // 生成的输入类
    private InputAction moveAction;   // player/Move
    private SHSariaConfig shSariaConfig;

    private bool hasStartedIdleAnim = false;

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);
    }

    public override void Enter()
    {
        var motionSS = player.CurrentMotion;
        if (motionSS.justLanded && motionSS.landHoldTime <= 0.15f)
        {
            player.PlayAnimation("JumpLand", 1f, false, 0.15f);
            hasStartedIdleAnim = false;
        }
        else
        {
            player.PlayAnimation("Idle", 1f, false, 0.25f);
            hasStartedIdleAnim = true;
        }

        Debug.Log("进入IdleState");

    }

    public override void Update()
    {
        var motionSS = player.CurrentMotion;
        if (!hasStartedIdleAnim && motionSS.landHoldTime > 0.15f)
        {
            player.PlayAnimation("Idle", 1f, false, 0.25f);
            hasStartedIdleAnim = true;
        }
    }
}
