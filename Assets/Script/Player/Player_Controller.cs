using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;
using System.Runtime.CompilerServices;
using UnityEditor.Rendering.LookDev;

/// <summary>
/// Player_Controller
/// -------------------------------------------------------------------------
/// 职责：
/// - 作为玩家的中枢控制器，协调动画、状态机和配置。
/// - 初始化 Animation_Contorller 和状态机（StateMachine）。
/// - 提供外部接口：播放单动画、播放 Blend 动画、设置 Blend 权重。
/// - 对状态机进行状态切换（Idle、Move等）。
/// - 提供相位锁接口：Enable / Update / Disable，用于步频同步。
///
/// 主要结构：
/// - Awake：自动补齐 Animation_Contorller 引用。
/// - Start/Init：初始化动画控制器、状态机，并进入默认状态 Idle。
/// - ChangeState：封装状态切换逻辑，基于 PlayerState 枚举。
/// - PlayAnimation / PlayBlendAnimation：通过配置获取 AnimationClip 并播放。
/// - SetBlendWeight：传递权重到 Animation_Contorller。
/// - 相位锁接口：对 Animation_Contorller 的 Enable/Update/Disable 封装。
///
/// 注意事项：
/// - Animation_Contorller 必须在 Player 的子物体上存在，否则无法初始化。
/// - shSariaConfig 需在 Inspector 赋值，否则无法根据名字找到动画。
/// - modelTransform 必须正确拖入，用于控制角色外观部分旋转。
/// -------------------------------------------------------------------------
/// </summary>

public class Player_Controller : SingletonMono<Player_Controller>, IStateMachineOwner
{
    [SerializeField] Animation_Contorller animation_Contorller;
    [SerializeField] private SHSariaConfig shSariaConfig;
    public SHSariaConfig ShSariaConfig => shSariaConfig;// 方便外部访问配置

    [SerializeField] private Transform modelTransform;//把模型部分拖进来，以防旋转等影响player controller
    public Transform ModelTransform => modelTransform;
    private StateMachine stateMachine;
    private PlayerState playerState; // 玩家的当前状态标识

    private void Awake()
    {
        // 自动补齐引用，防止忘记拖
        if (animation_Contorller == null)
        {
            animation_Contorller = GetComponentInChildren<Animation_Contorller>();
        }
        // 如果 Animation_Contorller 里需要 Animator，一定要在它的脚本里也做空引用检查
    }
    private void Start()
    {
        Init();
    }
    public void Init()
    {
        // 1) 先初始化动画控制器（若你在 Animation_Contorller 里有 Init 方法的话）
        if (animation_Contorller == null)
        {
            Debug.LogError("[Player_Controller] animation_Contorller 未赋值或未找到组件。请在 Player 上添加/拖拽 Animation_Contorller。");
            return; // 不能继续
        }
        // 如果你的 Animation_Contorller 有 Init()，在这里调用：
        animation_Contorller.Init();

        // 2) 从对象池取状态机
        stateMachine = PoolSystem.GetObject<StateMachine>() ?? new StateMachine();
        stateMachine.Init(this);

        // 3) 进入默认状态
        ChangeState(PlayerState.Idle);

        // 额外：检查配置
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 未赋值。后续 PlayAnimation(\"Idle\") 可能找不到动画。");
        }
    }

    /// <summary>
    /// 修改状态标识
    /// </summary>
    /// <param name="playerState"></param>
    public void ChangeState(PlayerState playerState)
    {
        this.playerState = playerState;
        switch (playerState)
        {
            case PlayerState.Idle:
                stateMachine.ChangeState<Player_IdleState>();
                break;
            case PlayerState.Move:
                stateMachine.ChangeState<Player_MoveState>();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationClipName"></param>
    public void PlayAnimation(string animationClipName, float speed = 1, bool refreshAnimation = false, float transitionFixedTime = 0.25f)
    {
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 为 null，无法根据名字获取动画。");
            return;
        }

        var clip = shSariaConfig.GetAnimationByName(animationClipName);
        if (clip == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{animationClipName}\" 的 AnimationClip。");
            return;
        }

        animation_Contorller.PlaySingleAnimation(clip, speed, refreshAnimation, transitionFixedTime);
    }

    /// <summary>
    /// 播放blend动画
    /// </summary>
    public void PlayBlendAnimation(string clip1Name, string clip2Name, float speed = 1f, float transitionFixedTime = 0.25f)
    {
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 为 null，无法根据名字获取动画。");
            return;
        }

        AnimationClip clip1 = shSariaConfig.GetAnimationByName(clip1Name);
        AnimationClip clip2 = shSariaConfig.GetAnimationByName(clip2Name);

        if (clip1 == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{clip1Name}\" 的 AnimationClip。");
            return;
        }

        if (clip2 == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{clip2Name}\" 的 AnimationClip。");
            return;
        }

        animation_Contorller.PlayBlendAnimation(clip1, clip2, speed, transitionFixedTime);
    }

    /// <summary>
    /// 设置blend动画的权重
    /// </summary>
    /// <param name="clip1Weight"></param>
    public void SetBlendWeight(float clip1Weight)
    {
        animation_Contorller.SetBlendWeight(clip1Weight);
    }


    /// <summary>启用 Walk/Run 的相位锁（可选初相位）。</summary>
    public void EnableBlendPhaseLock(float? initPhase01 = null) => animation_Contorller.EnablePhaseLockForWalkRun(initPhase01);
    
    /// <summary>按当前 Walk 权重推进相位（每帧调用）。</summary>
    public void UpdateBlendPhaseLock(float walkWeight)        => animation_Contorller.UpdatePhaseLockForWalkRun(walkWeight);
    
    /// <summary>关闭相位锁，恢复自动播放速度（默认1,1；如需自定义可传参）。</summary>
    public void DisableBlendPhaseLock(float s0=1f,float s1=1f)=> animation_Contorller.DisablePhaseLockForWalkRun(s0,s1);
}
