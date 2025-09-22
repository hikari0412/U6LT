using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;
using System.Runtime.CompilerServices;

public class Player_Controller : SingletonMono<Player_Controller>,IStateMachineOwner
{
    [SerializeField]Animation_Contorller animation_Contorller;
    [SerializeField] private SHSariaConfig shSariaConfig;  
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
    public void PlayAnimation(string animationClipName , float fixedTime = 0.25f)
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

        animation_Contorller.PlayAnimation(clip, fixedTime);
    }

}
