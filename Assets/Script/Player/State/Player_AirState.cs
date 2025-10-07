using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;


public class Player_AirState : PlayerStateBase
{

    public override void Init(IStateMachineOwner owner)
    {

        base.Init(owner);
    }

    public override void Enter()
    {
        var motionSS = player.CurrentMotion;

        player.PlayAnimation("JumpLoop", 1f, false, 0.5f);

        Debug.Log("进入AirState");

    }

    public override void Update()
    {

    }


}