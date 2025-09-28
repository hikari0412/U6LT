using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;

/// <summary>
/// Animation_Contorller
/// -------------------------------------------------------------------------
/// 职责：
/// 1. 初始化 PlayableGraph，并绑定到 Animator。
/// 2. 管理主混合器（mixer），支持：
///    - 普通动画播放（双通道，crossfade 淡入淡出）
///    - Blend 动画播放（多通道混合，实时调整权重）
/// 3. 提供过渡协程 TransitionAnimation，控制两个输入之间的权重平滑切换。
/// 4. 提供 ResetBlend / SetBlendWeight 等工具方法，用于管理 blend 动画。
///
/// 主要结构：
/// - Fields 区域：存储 Animator、PlayableGraph、mixer、clipPlayable、协程等引用
/// - Init：初始化 graph 和 mixer，绑定 Animator
/// - PlayAnimation：普通动画的播放与淡入淡出
/// - TransitionAnimation：统一的过渡协程
/// - PlayBlendAnimation：播放/切入 Blend 动画（mixer input 2）
/// - Blend Utils：管理 blendMixer，重置、创建、设置权重
/// - Lifecycle：在 OnDisable 时销毁 Graph
///
/// 使用方式示例：
/// - PlayAnimation(idleClip, 1f, false, 0.25f);  // 普通 crossfade
/// - PlayBlendAnimation(new List<AnimationClip>{ idle, walk }, 1f, 0.25f);
///   SetBlendWeight(new List<float>{ 1f - t, t }); // 按速度实时混合 Idle/Walk
///
/// 注意事项：
/// - Animator 必须挂在物体上
/// - PlayableGraph 使用前必须先 Init()（首次调用 Play/Blend 方法会自动 Init）
/// - Graph 的 input 数：0/1 = 普通双通道，2 = Blend 副混合器入口
/// - 所有 if/for/while 都加了大括号，避免维护时出错
/// -------------------------------------------------------------------------
/// </summary>


public class Animation_Contorller : MonoBehaviour
{
    #region Fields

    [SerializeField] private Animator animator;

    private PlayableGraph graph;
    public AnimationMixerPlayable mixer;                     // 主混合器（3路：0/1=普通双通道，2=Blend副混合器入口）

    private AnimationClipPlayable clipPlayable1;             // mixer input 0
    private AnimationClipPlayable clipPlayable2;             // mixer input 1
    private bool currentIsClipPlayable1 = true;              // 当前主通道是不是 input0
    private Coroutine transitionCoroutine;

    /*********************************** blend 模式相关 *******************************************/
    public AnimationMixerPlayable blendMixer;                // 用于 blend 的副混合器（挂在 mixer input 2）
    private bool currentIsBlend;                             // 当前是否处于 blend 模式（mixer 使用 input 2）
    private readonly List<AnimationClipPlayable> blendClipPlayables = new List<AnimationClipPlayable>();
    private int blendInputCount = 0;

    #endregion

    #region Init初始化

    public void Init()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Animation_Contorller] 找不到 Animator。");
                return;
            }
        }

        // 创建图
        graph = PlayableGraph.Create("Animation_Contorller");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        // 主混合器：3 输入（0/1=普通，2=blend 副混合器入口）
        mixer = AnimationMixerPlayable.Create(graph, 3);
        mixer.SetInputWeight(0, 0f);
        mixer.SetInputWeight(1, 0f);
        mixer.SetInputWeight(2, 0f);

        // 绑定到 Animator
        var playableOutput = AnimationPlayableOutput.Create(graph, "Animation", animator);
        playableOutput.SetSourcePlayable(mixer);
    }

    #endregion

    #region Play Animation (普通动画播放/过渡)

    /// <summary>
    /// 播放单个动画（普通 crossfade 流程）
    /// </summary>
    public void PlayAnimation(AnimationClip animationClip, float speed = 1f, bool refreshAnimation = false, float transitionFixedTime = 0.25f)
    {
        if (animationClip == null)
        {
            return;
        }

        // 自检 Animator / Graph
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            Debug.LogError("[Animation_Contorller] Animator 为 null，无法播放动画。");
            return;
        }
        if (!graph.IsValid())
        {
            Init();
            if (!graph.IsValid())
            {
                Debug.LogError("[Animation_Contorller] PlayableGraph 初始化失败。");
                return;
            }
        }

        // 首次播放：接到 slot0，权重设满
        if (!clipPlayable1.IsValid() && !clipPlayable2.IsValid() && !currentIsBlend)
        {
            clipPlayable1 = AnimationClipPlayable.Create(graph, animationClip);
            clipPlayable1.SetSpeed(speed);
            graph.Connect(clipPlayable1, 0, mixer, 0);

            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            mixer.SetInputWeight(2, 0f);

            currentIsClipPlayable1 = true;
            if (!graph.IsPlaying())
            {
                graph.Play();
            }
            return;
        }

        // 去抖：主通道已是同一剪辑且满权重 → 返回
        if (!currentIsBlend)
        {
            if (currentIsClipPlayable1)
            {
                if (clipPlayable1.IsValid() &&
                    clipPlayable1.GetAnimationClip() == animationClip &&
                    mixer.GetInputWeight(0) >= 0.999f)
                {
                    return;
                }
            }
            else
            {
                if (clipPlayable2.IsValid() &&
                    clipPlayable2.GetAnimationClip() == animationClip &&
                    mixer.GetInputWeight(1) >= 0.999f)
                {
                    return;
                }
            }
        }

        // 确定目标通道（普通模式：在 0 和 1 之间切换；若当前是 blend 模式，则从 2 过渡到该通道）
        bool fromSlot1 = currentIsClipPlayable1;
        int dst = fromSlot1 ? 1 : 0;               // 目标通道
        int fromIndex = currentIsBlend ? 2 : (fromSlot1 ? 0 : 1);

        // 将目标通道接上新动画，并把其初始权重设为 0
        if (mixer.GetInput(dst).IsValid())
        {
            graph.Disconnect(mixer, dst);
        }

        var newPlayable = AnimationClipPlayable.Create(graph, animationClip);
        newPlayable.SetSpeed(speed);
        graph.Connect(newPlayable, 0, mixer, dst);
        mixer.SetInputWeight(dst, 0f);
        if (dst == 0)
        {
            clipPlayable1 = newPlayable;
        }
        else
        {
            clipPlayable2 = newPlayable;
        }

        // 停掉旧的过渡协程
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        // 读当前源权重（若是从 blend 来，则源=2）
        float startWeight = Mathf.Clamp01(mixer.GetInputWeight(fromIndex));

        // 先对齐双方权重和为 1（避免第一帧跳变）
        mixer.SetInputWeight(fromIndex, startWeight);
        mixer.SetInputWeight(dst, 1f - startWeight);

        // 过渡（把 fromIndex → 0，toIndex → 1）
        transitionCoroutine = StartCoroutine(TransitionAnimation(fromIndex, dst, transitionFixedTime));

        // 状态标记更新（立刻退出 blend 标志，主通道标记在协程结尾也会被矫正）
        currentIsBlend = false;

        if (!graph.IsPlaying())
        {
            graph.Play();
        }
    }

    #endregion

    #region Transition 协程

    /// <summary>
    /// 统一的过渡协程：在主混合器的 fromIndex → toIndex 之间做权重过渡；fixedTime<=0 为硬切
    /// </summary>
    private IEnumerator TransitionAnimation(int fromIndex, int toIndex, float fixedTime)
    {
        // 若时长<=0，硬切
        if (fixedTime <= 0f)
        {
            mixer.SetInputWeight(fromIndex, 0f);
            mixer.SetInputWeight(toIndex, 1f);
        }
        else
        {
            float start = Mathf.Clamp01(mixer.GetInputWeight(fromIndex));
            float t = 0f;
            while (t < fixedTime)
            {
                t += Time.deltaTime;
                float w = Mathf.Lerp(start, 0f, Mathf.InverseLerp(0f, fixedTime, t));
                mixer.SetInputWeight(fromIndex, w);
                mixer.SetInputWeight(toIndex, 1f - w);
                yield return null;
            }
            mixer.SetInputWeight(fromIndex, 0f);
            mixer.SetInputWeight(toIndex, 1f);
        }

        // 过渡完成后矫正“当前主通道”标记
        if (toIndex == 0 || toIndex == 1)
        {
            currentIsClipPlayable1 = (toIndex == 0);
        }

        // 若切到了 blend（toIndex==2），标记处于 blend 模式
        currentIsBlend = (toIndex == 2);
        transitionCoroutine = null;
    }

    #endregion

    #region Play Blend Animation播放/切入 Blend 动画

    /// <summary>
    /// 播放（或切入）Blend 动画：clips 会被挂到 blendMixer（mixer 的 input 2），并过渡过去
    /// </summary>
    public void PlayBlendAnimation(List<AnimationClip> clips, float speed = 1f, float transitionFixedTime = 0.25f)
    {
        if (clips == null || clips.Count == 0)
        {
            return;
        }

        // 确保图与主混合器
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            Debug.LogError("[Animation_Contorller] Animator 为 null。");
            return;
        }
        if (!graph.IsValid())
        {
            Init();
            if (!graph.IsValid())
            {
                Debug.LogError("[Animation_Contorller] Graph 初始化失败。");
                return;
            }
        }

        // 初始化/重置 blend 副混合器并连接到 mixer 的 input 2
        ResetBlend(clips.Count);

        // 把 clips 装到 blendMixer
        for (int i = 0; i < clips.Count; i++)
        {
            CreateAndConnectBlendPlayable(clips[i], i, speed);
            // 初始：第0路权重=1，其它=0（可在外部 SetBlendWeight 再调整）
            blendMixer.SetInputWeight(i, i == 0 ? 1f : 0f);
        }

        // 停掉旧的过渡
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        // 从当前（0/1/或已在2）过渡到 2
        int fromIndex =
            currentIsBlend ? 2 :
            (currentIsClipPlayable1 ? 0 : 1);

        // 对齐初始相对权重，避免第一帧跳变
        float start = Mathf.Clamp01(mixer.GetInputWeight(fromIndex));
        mixer.SetInputWeight(fromIndex, start);
        mixer.SetInputWeight(2, 1f - start);

        transitionCoroutine = StartCoroutine(TransitionAnimation(fromIndex, 2, transitionFixedTime));

        if (!graph.IsPlaying())
        {
            graph.Play();
        }
    }

    #endregion

    #region Blend Utils管理 blendMixer

    /// <summary>
    /// 初始化/重置 blend 副混合器（输入数量=animationCount），并接到主混合器的 input 2
    /// </summary>
    private void ResetBlend(int animationCount)
    {
        // 清理旧的 blendMixer 连接
        if (mixer.GetInputCount() < 3)
        {
            // 重新创建 3 路主混合器（极端情况）
            var newMixer = AnimationMixerPlayable.Create(graph, 3);
            graph.Connect(mixer, 0, newMixer, 0); // ⚠️ TODO: 迁移逻辑未做，正常不会走到这里
            mixer = newMixer;
        }

        if (mixer.GetInput(2).IsValid())
        {
            graph.Disconnect(mixer, 2);
        }

        // 创建新的 blendMixer
        blendMixer = AnimationMixerPlayable.Create(graph, animationCount);
        graph.Connect(blendMixer, 0, mixer, 2);
        mixer.SetInputWeight(2, 0f);

        // 清空列表并记录输入数
        blendClipPlayables.Clear();
        blendInputCount = animationCount;
    }

    /// <summary>
    /// 将 clip 放入 blendMixer 的指定路（index），并设置播放速度
    /// </summary>
    private AnimationClipPlayable CreateAndConnectBlendPlayable(AnimationClip clip, int index, float speed)
    {
        var clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetSpeed(speed);
        blendClipPlayables.Add(clipPlayable);
        graph.Connect(clipPlayable, 0, blendMixer, index);
        return clipPlayable;
    }

    /// <summary>
    /// 设置 blendMixer 的各路权重（长度取 min）
    /// </summary>
    public void SetBlendWeight(List<float> weightList)
    {
        if (!blendMixer.IsValid())
        {
            return;
        }
        int n = Mathf.Min(weightList.Count, blendInputCount);
        for (int i = 0; i < n; i++)
        {
            blendMixer.SetInputWeight(i, weightList[i]);
        }
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    #endregion
}
