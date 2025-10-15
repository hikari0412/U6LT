using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;


public class Player_AirState : PlayerStateBase
{
    //动画播放标记
    private bool hasStartedJumpLoopAnim = false;

    public override void Init(IStateMachineOwner owner)
    {

        base.Init(owner);
    }

    public override void Enter()
    {
        var motionSS = player.CurrentMotion;

        if (motionSS.justJumped && motionSS.jumpBottonDown && motionSS.airHoldTime <= 0.25f)
        {
            player.PlayAnimation("JumpStart",0.15f);
            hasStartedJumpLoopAnim = false;
        }
        else
        {
            player.PlayAnimation("JumpLoop",0.25f);
            hasStartedJumpLoopAnim = true;
        }

        Debug.Log("进入AirState");

    }

    public override void Update()
    {
        var motionSS = player.CurrentMotion;
        if (!hasStartedJumpLoopAnim && motionSS.airHoldTime > 0.25f)
        {
            player.PlayAnimation("JumpLoop", 0.25f);
            hasStartedJumpLoopAnim = true;
        }
    }


}