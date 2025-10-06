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

    public override void Init(IStateMachineOwner owner)
    {

        base.Init(owner);

        ecmCharacter = player.GetComponent<Character>();
        if (ecmCharacter == null)
        {
            Debug.LogError("[MoveState] 请在 Player 上添加 ECM2.Character 组件。");
        }
        shSariaConfig = player.ShSariaConfig;
        input = new InputControls();

        moveAction = input.player.Move;
    }

    public override void Enter()
    {
        input?.Enable();

        player.PlayAnimation("Idle");

        Debug.Log("进入IdleState");

    }

    public override void Update()
    {
        float deltaTime = Time.deltaTime;

        Vector3 move = ecmCharacter.transform.InverseTransformDirection(ecmCharacter.GetMovementDirection());
        float forwardAmount = ecmCharacter.useRootMotion && ecmCharacter.GetRootMotionController()
                ? move.z
                : Mathf.InverseLerp(0.0f, ecmCharacter.GetMaxSpeed(), ecmCharacter.GetSpeed());
        
        // 计算地面速度（忽略Y）
        Vector3 vel = ecmCharacter.GetVelocity();
        vel.y = 0f;
        float horizSpeed = vel.magnitude;
        float speedXZ = Mathf.InverseLerp(0f, ecmCharacter.GetMaxSpeed(), horizSpeed);  // 0..1

        if (speedXZ >= 0.05f)
        {
            //切换状态
            player.ChangeState(PlayerState.Move);
            return;
        }
    }

    //离开当前状态时停止监听输入
    public override void Exit()
    {
        input?.Disable();
    }
}
