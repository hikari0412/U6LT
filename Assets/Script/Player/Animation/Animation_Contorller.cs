using GraphVisualizer;
using JKFrame;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Animation_Contorller（PlayableGraph 动画控制器）
/// -------------------------------------------------------------------------
/// 职责：
/// - 统一管理 Animator、PlayableGraph、主混合器，以及“当前/上一个”节点（单动画或混合节点）。
/// - 提供播放单动画 / 播放混合动画（2 路 / N 路）、跨动画过渡、全局速度设置等接口。
/// - 提供 Root Motion 回调（可选）：把 deltaPosition / deltaRotation 传给上层决定如何应用。
/// - 相位锁（Phase Lock）转发：仅在 Walk/Run 的双路混合时可用，调用由 BlendAnimationNode 承担。
///
/// 运行流程要点：
/// - 首次播放：接到 inputPort0，权重设为 1；后续切换到 inputPort1，并通过协程做互补权重过渡；每次过渡开头会交换 port 标记（0/1）。
/// - 过渡协程：权重从旧端口向 0 插值、从新端口向 1 插值；fixedTime<=0 视为硬切。
/// - 对象池：为 SingleAnimationNode / BlendAnimationNode 预热，频繁切换时避免 GC。
///
/// 注意事项：
/// - Init() 幂等：确保 Animator/Graph/Mixer 重复调用也安全；禁用/销毁后需重新 Init() 才能使用。
/// - 切换时请使用控制器提供的 Play* 接口；不要直接在外部操作 Graph 里的 Playable 结构。
/// -------------------------------------------------------------------------
/// </summary>

public class Animation_Contorller : MonoBehaviour
{
    #region ====================================== Fields & Properties ======================================
    [SerializeField] private Animator animator;

    // 用 fullPathHash（含层名）
    static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    static readonly int Move = Animator.StringToHash("Base Layer.Move");
    static readonly int JumpStart = Animator.StringToHash("Base Layer.Jump.JumpStart");
    static readonly int JumpLoop = Animator.StringToHash("Base Layer.Jump.JumpLoop");
    static readonly int JumpLand = Animator.StringToHash("Base Layer.Jump.JumpLand");
    int currentHash;

    #endregion

    #region ====================================== Lifecycle / Init & Teardown ======================================
    /// <summary>
    /// 初始化：确保 Animator / Graph / Mixer / 对象池 就绪（可重复调用，幂等）
    /// </summary>
    public void Init()
    {
        // 1) Animator 自检
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Animation_Contorller] 找不到 Animator（未拖引用且本物体上也没有）。");
                return;
            }
        }
    }

    void Awake()
    {
        // 首帧强制在 Idle，避免“第一帧姿势不定/抖动”
        animator.Play(Idle, 0, 0f);
        //animator.Update(0f);
        currentHash = Idle;
    }
    #endregion

    #region ====================================== 播放动画及animator参数设置 ======================================

    /// <summary>
    /// 播放单个动画
    /// </summary>
    public void PlayAnimation(string animationName, float transtionFixedTime = 0.25f, int layer = 0, float fixedTimeOffset = 0.0f)
    {
        animator.CrossFadeInFixedTime(animationName, transtionFixedTime, 0, float.NegativeInfinity);
    }

    public void setAnimatiorFloat(string name, float value)
    {
        animator.SetFloat(name, value);
    }

    public void setAnimatiorBool(string name, bool value)
    {
        animator.SetBool(name, value);
    }

    #endregion

    #region ====================================== 动画事件 ======================================

    private Dictionary<string, Action> eventDic = new Dictionary<string, Action>();

    public void AnimationEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            Debug.LogWarning("[AnimEvent] empty name");
            return;
        }

        if (!eventDic.TryGetValue(eventName, out var action) || action == null)
        {
            Debug.LogWarning($"[AnimEvent] no handler for '{eventName}'");
            return;
        }

        try
        {
            action();  // 如果回调出错，会在这里报行号
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AnimEvent] handler '{eventName}' threw: {ex}");
        }
    }

    //添加事件
    public void AddAnimationEvent(string eventName, Action action)
    {
        if (eventDic.TryGetValue(eventName, out Action _action))
        {
            _action += action;
        }
        else
        {
            eventDic.Add(eventName, action);
        }
    }

    //仅移除本事件（一个）
    public void RemoveAnimationEvent(string eventName, Action action)
    {
        if (eventDic.TryGetValue(eventName, out Action _action))
        {
            _action -= action;
        }
    }

    //移除事件（不允许再触发此事件）
    public void RemoveAnimationEvent(string eventName)
    {
        eventDic.Remove(eventName);
    }

    //删除所有的事件
    public void ClearAllActionEvent()
    {
        eventDic.Clear();
    }

    #endregion

    // #region ====================================== RootMotion 回调（可选） ======================================
    // private Action<Vector3, Quaternion> rootMotionAction;
    // private void OnAnimatorMove()
    // {
    //     rootMotionAction?.Invoke(animator.deltaPosition, animator.deltaRotation);
    // }
    // public void SetRootMotionAction(Action<Vector3, Quaternion> rootMotionAction)
    // {
    //     this.rootMotionAction = rootMotionAction;
    // }
    // public void ClearRootMotionAction()
    // {
    //     rootMotionAction = null;
    // }
    // #endregion

}
