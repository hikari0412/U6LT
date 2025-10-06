using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;


public class Player_MoveState : PlayerStateBase
{
    private Character ecmCharacter;   // 角色控制器（ECM2）
    private InputControls input;      // 生成的输入类
    private InputAction moveAction;   // player/Move
    private SHSariaConfig shSariaConfig;
    private float walkHold => shSariaConfig != null ? shSariaConfig.walkHold : 0.5f;
    private float rotateSpeed => shSariaConfig != null ? shSariaConfig.rotateSpeed : 10f;

    // 两路混合时：第一个动画（Walk）的当前权重（0..1）
    private float walkWeight = 0f;

    // 权重平滑时间常数（秒）
    private const float blendTau = 0.12f;

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

        player.PlayBlendAnimation("Walk", "Run", 1f, 0.25f);
        player.SetBlendWeight(0f);

        player.EnableBlendPhaseLock();

        Debug.Log("进入MoveState");
    }

    public override void Update()
    {
        float deltaTime = Time.deltaTime;

        Vector3 move = ecmCharacter.transform.InverseTransformDirection(ecmCharacter.GetMovementDirection());
        float forwardAmount = ecmCharacter.useRootMotion && ecmCharacter.GetRootMotionController()
                ? move.z
                : Mathf.InverseLerp(0.0f, ecmCharacter.GetMaxSpeed(), ecmCharacter.GetSpeed());

        float forwardAmountXZ = new Vector2(move.x, move.z).magnitude;

        if (forwardAmountXZ == 0)
        {
            player.DisableBlendPhaseLock();
            //切换状态
            player.ChangeState(PlayerState.Idle);
        }

        // === 根据ecm2的物理速度设置 Walk↔Run 混合比例 ===
        // 0..walkHold → 全 Walk；walkHold..1 → 线性过渡到 Run
        float speedRatio = Mathf.Clamp01(forwardAmountXZ);
        float runTarget = Mathf.Clamp01((speedRatio - walkHold) * 2f);
        float walkWeight = 1f - runTarget;

        // 平滑：指数平滑，避免权重剧变
        //float blendSmoothFactor = 1f - Mathf.Exp(-deltaTime / blendTau);
        //walkWeight = Mathf.Lerp(walkWeight, walkTarget, blendSmoothFactor);

        // 下发两路混合（只传第一路 Walk 的权重）
        player.SetBlendWeight(walkWeight);

        // 相位锁推进：用 Walk 权重作为推进/参考
        player.UpdateBlendPhaseLock(walkWeight);

        // === 平滑旋转模型（使用配置文件的 rotateSpeed） ===
        Quaternion targetRot = ecmCharacter.transform.rotation;

        player.ModelTransform.rotation = Quaternion.Slerp(
            player.ModelTransform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime);
    }

    public override void Exit()
    {
        player.DisableBlendPhaseLock();
        input?.Disable();
    }
}
