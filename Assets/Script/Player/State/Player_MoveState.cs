using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;


public class Player_MoveState : PlayerStateBase
{
    private SHSariaConfig shSariaConfig;
    private float rotateSpeed => shSariaConfig != null ? shSariaConfig.rotateSpeed : 10f;

    private bool leftNext = true;//用于脚步声换脚播放

    // 权重平滑时间常数（秒）
    private const float blendTau = 0.12f;

    //动画播放标记
    private bool hasStartedMoveAnim = false;

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);
        //注意！用到配置文件的要在这里由player注入
        shSariaConfig = player.ShSariaConfig;
    }

    public override void Enter()
    {
        player.AddAnimationEvent("FootStep", OnFootStep);

        var motionSS = player.CurrentMotion;
        if (motionSS.justLanded && motionSS.landHoldTime <= 0.15f)
        {
            player.PlayAnimation("JumpLand", 0.15f);
            hasStartedMoveAnim = false;
        }
        else
        {
            player.PlayAnimation("Move", 0.15f);
            hasStartedMoveAnim = true;
        }
        Debug.Log("进入MoveState");
    }

    public override void Update()
    {
        var motionSS = player.CurrentMotion;
        if (!hasStartedMoveAnim && motionSS.landHoldTime > 0.15f)
        {
            player.PlayAnimation("Move", 0.15f);
            hasStartedMoveAnim = true;
        }

        // 朝向：用速度方向而不是直接跟随 ecm transform
        if (motionSS.speedXZ > 0.001f)
        {
            Vector3 v = motionSS.speedWorld; v.y = 0f;
            var target = Quaternion.LookRotation(v);
            player.ModelTransform.rotation = Quaternion.Slerp(
                player.ModelTransform.rotation, target, rotateSpeed * Time.deltaTime);
        }
    }

    public override void Exit()
    {
        player.RemoveAnimationEvent("FootStep", OnFootStep);
    }

    private void OnFootStep()
    {
        if (shSariaConfig == null || shSariaConfig.FootStepAudioClips == null || shSariaConfig.FootStepAudioClips.Length == 0)
        {
            Debug.LogWarning("[Footstep] 配置未赋值或数组为空");
            return;
        }

        // 随机取一个脚步声
        int index = UnityEngine.Random.Range(0, shSariaConfig.FootStepAudioClips.Length);
        AudioClip clip = shSariaConfig.FootStepAudioClips[index];

        // 获取当前脚的世界坐标
        Transform foot = leftNext
            ? player.Animator.GetBoneTransform(HumanBodyBones.LeftFoot)
            : player.Animator.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 pos = foot.position;

        // 播放脚步声
        AudioSystem.PlayOneShot(clip, pos, false, 0.5f);

        // 下次换另一只脚
        leftNext = !leftNext;
    }
}
