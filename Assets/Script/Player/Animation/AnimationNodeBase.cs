using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JKFrame;

/// <summary>
/// AnimationNodeBase（动画节点基类）
/// -------------------------------------------------------------------------
/// 职责：
/// - 作为所有“可接入主混合器的节点”的基类，统一提供：
///   1) InputPort（本节点连接到外部混合器的端口号）
///   2) SetSpeed（让控制器在统一入口下发速度）
///   3) PushPool（回收到对象池）
///
/// 继承：
/// - SingleAnimationNode / BlendAnimationNode 等节点继承该类。
///
/// 注意事项：
/// - 控制器通过 InputPort 定位并断开本节点；PushPool 默认调用 JKFrame 的 PoolSystem。
/// -------------------------------------------------------------------------
/// </summary>
public abstract class AnimationNodeBase
{
    //本节点连接到外部混合器（通常是 Animation_Contorller.mixer）的哪个输入端口
    public int InputPort;
    // 由控制器下发速度（各节点自行决定如何应用到其内部 Playable）
    public abstract void SetSpeed(float speed);

    // 回收到对象池
    public virtual void PushPool()
    {
        PoolSystem.PushObject(this);
    }
}