using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections;
using Sirenix.OdinInspector;

public class Animation_Contorller : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private PlayableGraph graph;
    public AnimationMixerPlayable mixer;

    private AnimationClipPlayable clipPlayable1;  // mixer input 0
    private AnimationClipPlayable clipPlayable2;  // mixer input 1

    private bool isFirstPlay = true;
    private bool currentIsClipPlayable1 = true;   // 当前“主通道”为 input0吗？
    private Coroutine transitionCoroutine;

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

        // 创建混合器（2 输入）
        mixer = AnimationMixerPlayable.Create(graph, 2);

        // 绑定到 Animator
        var playableOutput = AnimationPlayableOutput.Create(graph, "Animation", animator);
        playableOutput.SetSourcePlayable(mixer);
    }

    /// <summary>
    /// 播放动画（fixedTime 为淡入淡出时长）
    /// </summary>
    public void PlayAnimation(AnimationClip animationClip, float fixedTime = 0.25f)
    {
        if (animationClip == null) return;

        // ★ 自检 Animator
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Animation_Contorller] Animator 为 null，无法播放动画。");
                return;
            }
        }

        // ★ 自检 Graph：未创建则补一次 Init()
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
        if (isFirstPlay)
        {
            clipPlayable1 = AnimationClipPlayable.Create(graph, animationClip);
            graph.Connect(clipPlayable1, 0, mixer, 0);

            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);

            isFirstPlay = false;
            currentIsClipPlayable1 = true;

            //如果 Graph 无效或者没有在播放，就启动 Graph，保证动画系统持续运作。
            if (!graph.IsValid() || !graph.IsPlaying()) graph.Play();
            return;
        }

        // —— 去抖：当前主通道已经满权重并且正在播的是同一剪辑 → 直接返回 ——
        if (currentIsClipPlayable1)
        {
            //如果clipPlayable1有效【clipPlayable1.IsValid()】
            //且clipPlayable1的动画剪辑与传入的animationClip相同【clipPlayable1.GetAnimationClip() == animationClip】
            //且此时动画完全是由 clipPlayable1 播放的，没有混合/过渡【mixer.GetInputWeight(0) >= 0.999f】，则直接返回
            if (clipPlayable1.IsValid() &&
                clipPlayable1.GetAnimationClip() == animationClip &&
                mixer.GetInputWeight(0) >= 0.999f)
                return;
        }
        else
        {
            if (clipPlayable2.IsValid() &&
                clipPlayable2.GetAnimationClip() == animationClip &&
                mixer.GetInputWeight(1) >= 0.999f)
                return;
        }

        //确定源/目标通道
        //dst 就是 下一次新动画要接入的目标通道索引。它的值只会是 0 或 1，用来告诉 mixer 把新动画放到哪一边。
        //如果 fromSlot1为true则说明当前在播放clip1（索引号0），所以下一次新动画要接入的目标通道索引为1。
        //语法为fromSlot1为真时，dst=1，否则为0。
        bool fromSlot1 = currentIsClipPlayable1;      // 源：当前主通道
        int dst = fromSlot1 ? 1 : 0;                  // 目标：另一侧

        // 将目标通道接上新动画，并把其初始权重设为 0
        if (mixer.GetInput(dst).IsValid())
            graph.Disconnect(mixer, dst);

        var newPlayable = AnimationClipPlayable.Create(graph, animationClip);
        graph.Connect(newPlayable, 0, mixer, dst);
        mixer.SetInputWeight(dst, 0f);

        if (dst == 0) clipPlayable1 = newPlayable; else clipPlayable2 = newPlayable;

        // 停掉旧的过渡协程
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);

        // 读取当前起始权重（可能是 1，也可能是被打断的 0.x）
        float startW = mixer.GetInputWeight(fromSlot1 ? 0 : 1);
        startW = Mathf.Clamp01(startW);

        // 先对齐双方权重和为 1（避免第一帧跳变）
        if (fromSlot1)
        {
            mixer.SetInputWeight(0, startW);
            mixer.SetInputWeight(1, 1f - startW);
        }
        else
        {
            mixer.SetInputWeight(1, startW);
            mixer.SetInputWeight(0, 1f - startW);
        }

        // 如果过渡时长 <= 0，直接硬切
        if (fixedTime <= 0f)
        {
            if (fromSlot1)
            {
                mixer.SetInputWeight(0, 0f);
                mixer.SetInputWeight(1, 1f);
            }
            else
            {
                mixer.SetInputWeight(1, 0f);
                mixer.SetInputWeight(0, 1f);
            }
            currentIsClipPlayable1 = !fromSlot1;
        }
        else
        {
            // 启动新的过渡：从 startW → 0，另一侧从 (1 - startW) → 1
            transitionCoroutine = StartCoroutine(CrossFadeFrom(startW, fixedTime, fromSlot1));
        }

        if (!graph.IsPlaying()) graph.Play();
    }

    /// <summary>
    /// 从当前权重 startW 做 crossfade：源通道从 startW→0，目标从 (1-startW)→1
    /// </summary>
    private IEnumerator CrossFadeFrom(float startW, float duration, bool fromSlot1)
    {
        startW = Mathf.Clamp01(startW);
        duration = Mathf.Max(0.0001f, duration);

        float w = startW;
        float speed = startW / duration; // 用时 duration 将 w 从 startW 降到 0

        while (w > 0f)
        {
            w = Mathf.Max(0f, w - Time.deltaTime * speed);

            if (fromSlot1)
            {
                mixer.SetInputWeight(0, w);
                mixer.SetInputWeight(1, 1f - w);
            }
            else
            {
                mixer.SetInputWeight(1, w);
                mixer.SetInputWeight(0, 1f - w);
            }

            yield return null;
        }

        // 过渡完成后，再更新“当前主通道”标记
        currentIsClipPlayable1 = !fromSlot1;
        transitionCoroutine = null;
    }

    private void OnDisable()
    {
        // 当脚本被禁用时，销毁 PlayableGraph
        if (graph.IsValid())
            graph.Destroy();
    }
}
