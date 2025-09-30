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
/// - 管理基于 PlayableGraph 的动画播放与切换。
/// - 支持单动画播放（AnimationClipPlayable）。
/// - 支持 Blend 混合播放（AnimationMixerPlayable），用于多动画之间的插值。
/// - 处理过渡：通过 CrossFade 在两个动画或混合之间平滑切换。
/// - 对外提供统一接口：PlayAnimation、PlayBlendAnimation、SetBlendWeight。
///
/// Blend 模式：
/// - 双动画 Blend：典型用于 Walk / Run 混合，根据权重平滑过渡。  
/// - 多动画 Blend（2~10 路）：可扩展用于 Idle / Walk / Jog / Run / Sprint 或不同动作的连续过渡。  
///   - 内部使用 AnimationMixerPlayable 动态连接多个 AnimationClipPlayable。  
///   - 权重通过 SetBlendWeight(List<float>) 传入，每个通道权重可独立设置。  
///   - 总权重不强制归一化，用户需在调用端自行控制合理性。  
///
/// 新增功能：相位锁（Phase Lock）
/// - 用一个统一的归一化相位（0..1）驱动多条 Blend 动画的播放时间。  
/// - Walk 与 Run 等长度不一致的循环动画可保持脚步相位同步，避免“单脚卡住”。  
/// - 接口：EnablePhaseLock、UpdatePhaseLock、DisablePhaseLock。  
///   - EnablePhaseLock：停止自动播放，初始化相位（可选指定初始值）。  
///   - UpdatePhaseLock：每帧按混合权重计算周期，推进相位并同步到所有轨道。  
///   - DisablePhaseLock：恢复自由播放速度。  
///
/// 主要结构：
/// - 初始化：创建 Graph、PlayableOutput，持有 Mixer 与 ClipPlayable。  
/// - PlayAnimation：播放单一动画，支持 CrossFade。  
/// - PlayBlendAnimation：播放多路动画（2~10），并管理权重。  
/// - SetBlendWeight：更新 BlendMixer 的输入权重。  
/// - 相位锁方法：Enable/Update/DisablePhaseLock，内部用 phase01 驱动时间。  
///
/// 注意事项：
/// - 相位锁启用后，Blend 动画的 speed 会被强制置零，由相位推进 SetTime 驱动。  
/// - 退出状态时务必调用 DisablePhaseLock，否则其它状态动画也会被锁定。  
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

    // —— 相位锁（Phase Lock）——
    private bool phaseLockEnabled = false;   // 是否启用归一化相位锁
    private double phase01 = 0.0;            // 0..1 的归一化相位（左脚着地→左脚着地）

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

    /// <summary>
    /// 播放（或切入）Blend 动画（仅两个blend）：clips 会被挂到 blendMixer（mixer 的 input 2），并过渡过去
    /// </summary>
    public void PlayBlendAnimation(AnimationClip clip1, AnimationClip clip2, float speed = 1f, float transitionFixedTime = 0.25f)
    {
        if (clip1 == null || clip2 == null)
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
        ResetBlend(2);

        // 把 clip1和2 装到 blendMixer
        CreateAndConnectBlendPlayable(clip1, 0, speed);
        CreateAndConnectBlendPlayable(clip2, 1, speed);
        // 初始：第0路权重=1，其它=0（可在外部 SetBlendWeight 再调整）
        blendMixer.SetInputWeight(0, 1);

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

    /// <summary>
    /// 设置 blendMixer 的权重（仅2个动画的情况）
    /// </summary>
    public void SetBlendWeight(float clip1Weight)
    {
        blendMixer.SetInputWeight(0, clip1Weight);
        blendMixer.SetInputWeight(1, 1 - clip1Weight);
    }

    /// <summary>
    /// 启用相位锁：暂停两条 Blend 轨的自动播放（speed=0），用统一相位驱动 SetTime()。
    /// 可选 initialPhase01（0..1）。若不传，则以通道0当前相位为基准。
    /// </summary>
    public void EnablePhaseLock(float? initialPhase01 = null)
    {
        if (!blendMixer.IsValid() || blendClipPlayables.Count < 2)
        {
            Debug.LogWarning("[Animation_Contorller] EnablePhaseLock 需要已建立的两条 blend 轨（索引0/1）。");
            return;
        }

        var p0 = blendClipPlayables[0];
        var p1 = blendClipPlayables[1];
        if (!p0.IsValid() || !p1.IsValid()) return;

        // 初始相位：外部给，或由通道0当前时间反推
        if (initialPhase01.HasValue)
        {
            phase01 = Mathf.Repeat(initialPhase01.Value, 1f);
        }
        else
        {
            var c0 = p0.GetAnimationClip();
            double t0 = p0.GetTime();
            phase01 = (c0 != null && c0.length > 0f) ? ((t0 % c0.length) / c0.length) : 0.0;
        }

        // 停止自动前进：相位由我们来驱动
        p0.SetSpeed(0);
        p1.SetSpeed(0);

        phaseLockEnabled = true;
        ApplyPhaseToBlendClips(); // 立刻对齐一次
    }

    /// <summary>
    /// 关闭相位锁，恢复两条轨的自动播放（你可指定恢复速度，默认1,1）。
    /// </summary>
    public void DisablePhaseLock(float speed0 = 1f, float speed1 = 1f)
    {
        if (blendMixer.IsValid() && blendClipPlayables.Count >= 2)
        {
            var p0 = blendClipPlayables[0];
            var p1 = blendClipPlayables[1];
            if (p0.IsValid()) { p0.SetSpeed(speed0); }
            if (p1.IsValid()) { p1.SetSpeed(speed1); }
        }
        phaseLockEnabled = false;
    }

    /// <summary>
    /// 将当前 phase01 同步到两条 Blend 轨（按各自长度映射绝对时间）
    /// </summary>
    private void ApplyPhaseToBlendClips()
    {
        if (!blendMixer.IsValid() || blendClipPlayables.Count < 2) return;

        var p0 = blendClipPlayables[0];
        var p1 = blendClipPlayables[1];
        if (!p0.IsValid() || !p1.IsValid()) return;

        var c0 = p0.GetAnimationClip();
        var c1 = p1.GetAnimationClip();
        if (c0 == null || c1 == null) return;

        double t0 = phase01 * c0.length;
        double t1 = phase01 * c1.length;

        p0.SetTime(t0);
        p1.SetTime(t1);
        // 提示：不需要手动 Evaluate，图在本帧更新时会生效
    }

    /// <summary>
    /// 每帧推进相位：mixedLength = walkWeight * len0 + (1-walkWeight) * len1
    /// phase01 += dt / mixedLength （保持步频平滑过渡）
    /// </summary>
    public void UpdatePhaseLock(float walkWeight)
    {
        if (!phaseLockEnabled) return;
        if (!blendMixer.IsValid() || blendClipPlayables.Count < 2) return;

        var c0 = blendClipPlayables[0].GetAnimationClip(); // Walk
        var c1 = blendClipPlayables[1].GetAnimationClip(); // Run
        if (c0 == null || c1 == null) return;

        float len0 = Mathf.Max(0.0001f, c0.length);
        float len1 = Mathf.Max(0.0001f, c1.length);

        walkWeight = Mathf.Clamp01(walkWeight);
        float runWeight = 1f - walkWeight;

        float mixedLength = walkWeight * len0 + runWeight * len1; // 混合周期
        phase01 = (phase01 + Time.deltaTime / mixedLength) % 1.0;

        ApplyPhaseToBlendClips();
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
