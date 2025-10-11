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

    //动画播放标记
    private bool hasStartedWalkRunAnim = false;

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
            hasStartedWalkRunAnim = false;
        }
        else
        {
            player.PlayBlendAnimation("Walk", "Run", 1f, 0.25f);
            player.SetBlendWeight(0f);

            player.EnableBlendPhaseLock();
            hasStartedWalkRunAnim = true;
        }


        Debug.Log("进入MoveState");
    }

    public override void Update()
    {
        var motionSS = player.CurrentMotion;
        if (!hasStartedWalkRunAnim && motionSS.landHoldTime > 0.15f)
        {
            player.PlayBlendAnimation("Walk", "Run", 1f, 0.25f);
            player.SetBlendWeight(0f);

            player.EnableBlendPhaseLock();
            hasStartedWalkRunAnim = true;
        }
        float dt = Time.deltaTime;
        var m = player.CurrentMotion;

        // 用“快照”里的速度比例（仅 XZ）做走↔跑混合
        float speedRatio = Mathf.Clamp01(m.speedRadio);       // 0..1
        float runTarget = Mathf.Clamp01((speedRatio - walkHold) * 2f);
        float walkTarget = 1f - runTarget;

        // 平滑
        float k = 1f - Mathf.Exp(-dt / blendTau);
        walkWeight = Mathf.Lerp(walkWeight, walkTarget, k);

        // 下发混合与相位锁推进
        player.SetBlendWeight(walkWeight);
        player.UpdateBlendPhaseLock(walkWeight);

        // 朝向：用速度方向而不是直接跟随 ecm transform
        if (m.speedXZ > 0.001f)
        {
            Vector3 v = m.speedWorld; v.y = 0f;
            var target = Quaternion.LookRotation(v);
            player.ModelTransform.rotation = Quaternion.Slerp(
                player.ModelTransform.rotation, target, rotateSpeed * dt);
        }
    }

    public override void Exit()
    {
        player.DisableBlendPhaseLock();
    }
}
