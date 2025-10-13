using JKFrame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// BlendAnimationNode（多动画混合节点）
/// -------------------------------------------------------------------------
/// 职责：
/// - 内部自建一个子混合器（blendMixer），将 2~N 个 AnimationClipPlayable 连接进去，
///   然后把 blendMixer 接到控制器的主混合器指定端口（InputPort）。
/// - 提供 SetBlendWeight（两路互补/多路列表）、SetSpeed（对所有子片）等能力。
/// - 相位锁（Phase Lock，仅 Walk/Run 双路时启用）：
///   - EnablePhaseLock：暂停两条子片的自动播放（speed=0），用统一 phase01 驱动 SetTime；
///   - UpdatePhaseLock：按当前 walkWeight 推进相位（混合周期 = wWalk*lenWalk + wRun*lenRun）；
///   - DisablePhaseLock：恢复自由播放速度。
///
/// 使用方式：
/// - 控制器在 PlayBlendAnimation(clip1, clip2) 或 PlayBlendAnimation(List<clip>) 时创建并初始化本节点；
/// - 上层通过控制器的 SetBlendWeight 接口传入权重；
/// - 若资源名包含 Walk/Run，可由控制器转发调用 Enable/Update/DisablePhaseLock。
///
/// 注意事项：
/// - 相位锁只在子片数 == 2 且资源名包含 Walk 与 Run 时启用（见 EnablePhaseLock 内部检查）。
/// - PushPool() 时会清理相位锁状态并清空子片列表；控制器会先断开连接再回收。
/// -------------------------------------------------------------------------
/// </summary>
public class BlendAnimationNode : AnimationNodeBase
{
    private AnimationMixerPlayable blendMixer;
    private List<AnimationClipPlayable> blendClipPlayableList = new List<AnimationClipPlayable>(10);

    // === 相位锁字段（仅 2 路 Walk/Run 使用） ===
    private bool phaseLockEnabled = false;
    private double phase01 = 0.0; // 0..1 左脚着地→左脚着地
    public int ClipCount => blendClipPlayableList.Count;

    /// <summary>
    /// 初始化：2~N 路混合
    /// </summary>
    public void Init(PlayableGraph graph, AnimationMixerPlayable outputMixer, List<AnimationClip> clips, float speed, int inputPort)
    {
        blendMixer = AnimationMixerPlayable.Create(graph, clips.Count);
        graph.Connect(blendMixer, 0, outputMixer, inputPort);
        this.InputPort = inputPort;
        for (int i = 0; i < clips.Count; i++)
        {
            CreateAndConnectBlendPlayable(graph, clips[i], i, speed);
        }
    }

    /// <summary>
    /// 初始化：2 路混合（典型：Walk/Run）
    /// </summary>
    public void Init(PlayableGraph graph, AnimationMixerPlayable outputMixer, AnimationClip clip1, AnimationClip clip2, float speed, int inputPort)
    {
        blendMixer = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(blendMixer, 0, outputMixer, inputPort);
        this.InputPort = inputPort;
        CreateAndConnectBlendPlayable(graph, clip1, 0, speed);
        CreateAndConnectBlendPlayable(graph, clip2, 1, speed);
    }

    /// <summary>
    /// 创建并连接一个子片到 blendMixer 的 index 端口
    /// </summary>
    private AnimationClipPlayable CreateAndConnectBlendPlayable(PlayableGraph graph, AnimationClip clip, int index, float speed)
    {
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(true);
        clipPlayable.SetSpeed(speed);
        blendClipPlayableList.Add(clipPlayable);
        graph.Connect(clipPlayable, 0, blendMixer, index);
        return clipPlayable;
    }
    
    /// <summary>
    /// 多路权重（与 clips 数一致；未强制归一化，调用端需自行控制）
    /// </summary>
    public void SetBlendWeight(List<float> weightList)
    {
        for (int i = 0; i < blendClipPlayableList.Count; i++)
        {
            blendMixer.SetInputWeight(i, weightList[i]);
        }
    }

    /// <summary>
    /// 双路互补权重（0 通道 = Walk，1 通道 = Run）
    /// </summary>
    public void SetBlendWeight(float clip1Weight)
    {
        blendMixer.SetInputWeight(0, clip1Weight);
        blendMixer.SetInputWeight(1, 1 - clip1Weight);
    }

/// <summary>
    /// 下发速度（对所有子片）
    /// </summary>
    public override void SetSpeed(float speed)
    {
        for (int i = 0; i < blendClipPlayableList.Count; i++)
        {
            blendClipPlayableList[i].SetSpeed(speed);
        }
    }

    /// <summary>
    /// 回收到对象库（清理相位锁与本地列表）
    /// </summary>
    public override void PushPool()
    {
        // 退出时务必关闭相位锁
        phaseLockEnabled = false;
        blendClipPlayableList.Clear();
        base.PushPool();
    }

    /// <summary>
    /// 启用相位锁：仅当 2 路且资源名包含 Walk/Run 时才启用
    /// </summary>
    public void EnablePhaseLock(float? initialPhase01 = null)
    {
        if (blendClipPlayableList.Count < 2) return;

        var p0 = blendClipPlayableList[0];
        var p1 = blendClipPlayableList[1];
        if (!p0.IsValid() || !p1.IsValid()) return;

        var c0 = p0.GetAnimationClip();
        var c1 = p1.GetAnimationClip();
        if (c0 == null || c1 == null) return;

        //名称检查：必须包含 Walk 和 Run
        string name0 = c0.name.ToLower();
        string name1 = c1.name.ToLower();
        bool isWalkRun = (name0.Contains("walk") && name1.Contains("run")) ||
                         (name0.Contains("run") && name1.Contains("walk"));

        if (!isWalkRun)
        {
            Debug.Log("[BlendAnimationNode] 相位锁未启用：子动画不是 Walk/Run。");
            return;
        }

        //初始化相位（外部给定或取当前 0 通道的相位）
        if (initialPhase01.HasValue)
        { phase01 = Mathf.Repeat(initialPhase01.Value, 1f); }
        else
        { phase01 = (c0.length > 0) ? ((p0.GetTime() % c0.length) / c0.length) : 0.0; }

        // 停止自动播放：交给相位锁驱动 SetTime    
        p0.SetSpeed(0);
        p1.SetSpeed(0);
        phaseLockEnabled = true;
        ApplyPhaseToTwoClips();

    }

    /// <summary>
    /// 关闭相位锁，恢复自由播放速度
    /// </summary>
    public void DisablePhaseLock(float speed0 = 1f, float speed1 = 1f)
    {
        if (blendClipPlayableList.Count >= 2)
        {
            var p0 = blendClipPlayableList[0];
            var p1 = blendClipPlayableList[1];
            if (p0.IsValid()) p0.SetSpeed(speed0);
            if (p1.IsValid()) p1.SetSpeed(speed1);
        }
        phaseLockEnabled = false;
    }

    /// <summary>
    /// 每帧推进相位：mixedLength = wWalk*lenWalk + wRun*lenRun
    /// </summary>
    public void UpdatePhaseLock(float walkWeight)
    {
        if (!phaseLockEnabled) return;
        if (blendClipPlayableList.Count < 2) return;

        var c0 = blendClipPlayableList[0].GetAnimationClip(); // Walk
        var c1 = blendClipPlayableList[1].GetAnimationClip(); // Run
        if (c0 == null || c1 == null) return;

        float len0 = Mathf.Max(0.0001f, c0.length);
        float len1 = Mathf.Max(0.0001f, c1.length);

        walkWeight = Mathf.Clamp01(walkWeight);
        float runWeight = 1f - walkWeight;

        // 按当前权重得到“混合步频”的周期
        float mixedLength = walkWeight * len0 + runWeight * len1;
        phase01 = (phase01 + Time.deltaTime / mixedLength) % 1.0;

        ApplyPhaseToTwoClips();
    }

    /// <summary>
    /// 将当前 phase01 同步到两条子片（SetTime）
    /// </summary>
    private void ApplyPhaseToTwoClips()
    {
        var p0 = blendClipPlayableList[0];
        var p1 = blendClipPlayableList[1];
        if (!p0.IsValid() || !p1.IsValid()) return;

        var c0 = p0.GetAnimationClip();
        var c1 = p1.GetAnimationClip();
        if (c0 == null || c1 == null) return;

        p0.SetTime(phase01 * c0.length);
        p1.SetTime(phase01 * c1.length);
        // 不需要手动 Evaluate，图会在本帧更新
    }
}
