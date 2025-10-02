using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine;
using JKFrame;
/// <summary>
/// SingleAnimationNode（单动画节点）
/// -------------------------------------------------------------------------
/// 职责：
/// - 持有一个 AnimationClipPlayable，并把它连接到外部传入的输出混合器（outputMixer）。
/// - 提供统一的 SetSpeed 接口供控制器下发全局速度。
///
/// 使用方式（由 Animation_Contorller 管理）：
/// - Init(graph, outputMixer, clip, speed, inputPort) 后，outputMixer 的 inputPort 端口会接入这个 clipPlayable。
/// - GetAnimationClip() 可用于“同剪辑去抖”（避免重复切换同一个动画）。
///
/// 注意事项：
/// - InputPort 用于记录该节点连接在 outputMixer 的哪个输入端口；销毁时控制器会用它来断开连接。
/// - 如果你发现“单动画播放的端口总是 0”，检查 Init 内部的 graph.Connect 目标端口（目前源码写死到 0 号端口，见下方 TODO）。
/// -------------------------------------------------------------------------
/// </summary>
public class SingleAnimationNode : AnimationNodeBase
{
    private AnimationClipPlayable clipPlayable;
    public void Init(PlayableGraph graph, AnimationMixerPlayable outputMixer, AnimationClip animationClip, float speed, int inputPort)
    {
        clipPlayable = AnimationClipPlayable.Create(graph, animationClip);
        clipPlayable.SetSpeed(speed);
        // 记录节点连接的端口号，控制器会在回收/销毁时用到
        InputPort = inputPort;
        // 将本节点连接到输出混合器
        graph.Connect(clipPlayable, 0, outputMixer, inputPort);
    }

    /// <summary>
    /// 获取当前 AnimationClip（用于控制器做“同剪辑去抖”）
    /// </summary>
    public AnimationClip GetAnimationClip()
    {
        return clipPlayable.GetAnimationClip();
    }

    /// <summary>
    /// 下发速度（全局由控制器管理时会调用）
    /// </summary>
    public override void SetSpeed(float speed)
    {
        clipPlayable.SetSpeed(speed);
    }
}