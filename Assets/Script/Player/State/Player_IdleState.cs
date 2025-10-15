using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;

public class Player_IdleState : PlayerStateBase
{
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
            player.PlayAnimation("JumpLand", 0.15f);
            hasStartedIdleAnim = false;
        }
        else
        {
            player.PlayAnimation("Idle", 0.25f);
            hasStartedIdleAnim = true;
        }

        Debug.Log("进入IdleState");

        player.setAnimatiorBool("isChangeBodyPosition", true);

    }

    public override void Update()
    {
        var motionSS = player.CurrentMotion;
        if (!hasStartedIdleAnim && motionSS.landHoldTime > 0.15f)
        {
            player.PlayAnimation("Idle", 0.25f);
            hasStartedIdleAnim = true;
        }
    }

    public override void Exit()
    {
        player.setAnimatiorBool("isChangeBodyPosition", false);
    }
}
