using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;

/// <summary>
/// 玩家待机状态
/// </summary>
public class Player_IdleState : PlayerStateBase
{
    private InputControls input;      // 新输入系统生成的输入类
    private InputAction moveAction;   // player/Move
    private InputAction jumpAction;

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);   //调用基类的init，如果不写这个player就不会被赋值，后面就会NullReference
        input = new InputControls();
        //取出 input 里 "player" 这个 ActionMap 下的 "Move" 动作，并缓存到 moveAction。
        //这个Move是指input系统里设置的Move动作，不是状态机的Move状态
        moveAction = input.player.Move;
        jumpAction = input.player.Jump;
    }

    public override void Enter()
    {
        //如果 input != null，就调用 Enable()；
        //如果 input == null，什么也不做，不会抛 NullReferenceException。
        // Enable() 是 Unity 新输入系统里的方法，用来激活所有在 InputControls 里定义的输入动作。
        // 没有调用 Enable() 的话，moveAction.ReadValue<Vector2>() 永远读不到输入。
        input?.Enable();
        //播放待机动作
        player.PlayAnimation("Idle");
    }

    public override void Update()
    {
        if (jumpAction.WasPerformedThisFrame())
        {
            player.ChangeState(PlayerState.Jump);
        }

        //检测玩家的输入
        Vector2 move = moveAction.ReadValue<Vector2>();
        float h = move.x;
        float v = move.y;

        if (h != 0 || v != 0)
        {
            //切换状态
            player.ChangeState(PlayerState.Move);
        }
    }

    //离开当前状态时停止监听输入
    public override void Exit()
    {
        input?.Disable();
    }
}