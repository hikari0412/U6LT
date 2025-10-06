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

    private PlayableGraph graph;
    public AnimationMixerPlayable mixer;// 主混合器（3路：0/1=普通双通道，2=Blend副混合器入口）

    private AnimationNodeBase previousNode;//上一个节点
    private AnimationNodeBase currentNode;//当前节点
    private int inputPort0 = 0;// 标记“新节点”接入端口（过渡时会交换）
    private int inputPort1 = 1;// 标记“旧节点”接入端口（过渡时会交换）
    private Coroutine transitionCoroutine;

    private float speed;
    /// <summary>
    /// 全局速度（会透传到 currentNode 的 SetSpeed）
    /// </summary>
    public float Speed
    {
        get => speed;
        set
        {
            speed = value;
            if (currentNode != null) currentNode.SetSpeed(speed);
        }
    }

    // —— 相位锁（Phase Lock）——（由 BlendAnimationNode 实现，控制器只做“Walk/Run 时”的调用转发）
    private bool phaseLockEnabled = false;   // 是否启用归一化相位锁
    private double phase01 = 0.0;            // 0..1 的归一化相位（左脚着地→左脚着地）
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

        // 2) Graph 自检（重复调用 Init 不会重复创建）
        if (!graph.IsValid())
        {
            // 创建图
            graph = PlayableGraph.Create("Animation_Contorller");
            // 设置图的时间模式
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        }

        // 3) Mixer 自检（3 路：0/1=普通，2=blend 副混合器入口）
        if (!mixer.IsValid())
        {
            // 主混合器：3 输入（0/1=普通，2=blend 副混合器入口）
            mixer = AnimationMixerPlayable.Create(graph, 3);
            // 绑定输出到 Animator
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);

            // 初始权重清零
            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 0f);
            mixer.SetInputWeight(2, 0f);
        }

        if (!graph.IsPlaying())
        {
            graph.Play();
        }

        //对象池初始化
        PoolSystem.InitObjectPool<SingleAnimationNode>(maxCapacity: 16, defaultQuantity: 4); // 预热4个
        PoolSystem.InitObjectPool<BlendAnimationNode>(maxCapacity: 8, defaultQuantity: 2); // 预热2个

    }

    /// <summary>
    /// 从混合器断开并回收到对象池（由控制器统一调用）
    /// </summary>
    public void DestoryNode(AnimationNodeBase node)
    {
        if (node != null)
        {
            graph.Disconnect(mixer, node.InputPort);
            node.PushPool();
        }
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    private void OnDisable()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
    #endregion

    #region ====================================== Transition（统一过渡协程） ======================================
    private void StartTransitionAnimation(float fixedTime)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        transitionCoroutine = StartCoroutine(TransitionAnimation(fixedTime));
    }


    /// <summary>
    /// 统一的过渡协程：在主混合器的 fromIndex → toIndex 之间做权重过渡；fixedTime<=0 为硬切
    /// </summary>
    private IEnumerator TransitionAnimation(float fixedTime)
    {
        //交换端口号
        int temp = inputPort0;
        inputPort0 = inputPort1;
        inputPort1 = temp;

        // 若时长<=0，硬切（因为前面已经交换过端口号了，所以1是旧节点，0是新节点）
        if (fixedTime <= 0f)
        {
            mixer.SetInputWeight(inputPort1, 0f);
            mixer.SetInputWeight(inputPort0, 1f);
        }
        else
        {
            // 平滑：旧权重从 start → 0，新权重做 1 - 旧
            float start = Mathf.Clamp01(mixer.GetInputWeight(inputPort1));
            float t = 0f;
            while (t < fixedTime)
            {
                t += Time.deltaTime;
                float w = Mathf.Lerp(start, 0f, Mathf.InverseLerp(0f, fixedTime, t));
                mixer.SetInputWeight(inputPort1, w);
                mixer.SetInputWeight(inputPort0, 1f - w);
                yield return null;
            }
            mixer.SetInputWeight(inputPort1, 0f);
            mixer.SetInputWeight(inputPort0, 1f);
        }
        transitionCoroutine = null;
    }
    #endregion

    #region ====================================== Play：单动画 ======================================

    /// <summary>
    /// 播放单个动画（可选：refreshAnimation 跳过“同剪辑去抖”）
    /// </summary>
    public void PlaySingleAnimation(AnimationClip animationClip, float speed = 1, bool refreshAnimation = false, float transtionFixedTime = 0.25f)
    {
        // —— 保护：空片直接返回
        if (animationClip == null)
        {
            Debug.LogWarning("[Animation_Contorller] PlaySingleAnimation 传入的 AnimationClip 为 null。");
            return;
        }

        // —— 关键：确保 Animator/Graph/Mixer 都已就绪
        if (animator == null || !graph.IsValid() || !mixer.IsValid())
        {
            Init();
            if (animator == null || !graph.IsValid() || !mixer.IsValid())
            {
                Debug.LogError("[Animation_Contorller] Graph/Mixer 未成功初始化，无法播放。");
                return;
            }
        }

        SingleAnimationNode singleAnimationNode = null;
        if (currentNode == null) //首次播放
        {
            singleAnimationNode = PoolSystem.GetObject<SingleAnimationNode>();//因为动画会频繁切换所以使用对象池
            singleAnimationNode.Init(graph, mixer, animationClip, speed, inputPort0);
            mixer.SetInputWeight(inputPort0, 1);
        }
        else//不是首次播放则启动过渡流程
        {
            SingleAnimationNode preNode = currentNode as SingleAnimationNode;
            //通过强转为SingleMode判断当前节点是不是blend节点（是的话强转失败为null），
            //只有在都为SingleMode单个动画点、refreshAnimation为false且当前动画和新播放的动画Clip一样的情况才不需要刷新直接return。
            if (preNode != null && !refreshAnimation && preNode.GetAnimationClip() == animationClip) return;
            //断开可能被占用的Node，并把这个旧的Node放进对象池
            DestoryNode(previousNode);
            singleAnimationNode = PoolSystem.GetObject<SingleAnimationNode>();
            singleAnimationNode.Init(graph, mixer, animationClip, speed, inputPort1);//因为每一次协程都交换了端口号，所以1号一定是新动画端口
            previousNode = currentNode;
            StartTransitionAnimation(transtionFixedTime);
        }

        this.speed = speed; //只需要把记录值更新一下即可，在Init时实际每个动画都已经设置好了速度，不用使用属性再赋值
        currentNode = singleAnimationNode;
        if (graph.IsPlaying() == false)
        { graph.Play(); }
    }
    #endregion

    #region ====================================== Play：混合动画 ======================================
    /// <summary>
    /// 播放混合动画（2个混合）
    /// </summary>
    public void PlayBlendAnimation(AnimationClip clip1, AnimationClip clip2, float speed = 1, float transitionFixedTime = 0.25f)
    {
        BlendAnimationNode blendAnimationNode = PoolSystem.GetObject<BlendAnimationNode>();
        // 如果是第一次播放，不存在过渡
        if (currentNode == null)
        {
            blendAnimationNode.Init(graph, mixer, clip1, clip2, speed, inputPort0);
            mixer.SetInputWeight(inputPort0, 1);
        }
        else
        {
            DestoryNode(previousNode);
            blendAnimationNode.Init(graph, mixer, clip1, clip2, speed, inputPort1);
            previousNode = currentNode;
            StartTransitionAnimation(transitionFixedTime);
        }
        this.speed = speed;
        currentNode = blendAnimationNode;
        if (graph.IsPlaying() == false) graph.Play();
    }

    /// <summary>
    /// 播放混合动画（数组(2个以上)混合）
    /// </summary>
    public void PlayBlendAnimation(List<AnimationClip> clips, float speed = 1, float transitionFixedTime = 0.25f)
    {
        BlendAnimationNode blendAnimationNode = PoolSystem.GetObject<BlendAnimationNode>();
        // 如果是第一次播放，不存在过渡
        if (currentNode == null)
        {
            blendAnimationNode.Init(graph, mixer, clips, speed, inputPort0);
            mixer.SetInputWeight(inputPort0, 1);
        }
        else
        {
            DestoryNode(previousNode);
            blendAnimationNode.Init(graph, mixer, clips, speed, inputPort1);
            previousNode = currentNode;
            StartTransitionAnimation(transitionFixedTime);
        }
        this.speed = speed;
        currentNode = blendAnimationNode;
        if (graph.IsPlaying() == false) graph.Play();
    }

    #endregion

    #region ====================================== Blend Weight（对外接口） ======================================

    public void SetBlendWeight(List<float> weightList)
    {
        if (currentNode is BlendAnimationNode b) b.SetBlendWeight(weightList);
    }
    public void SetBlendWeight(float clip1Weight)
    {
        if (currentNode is BlendAnimationNode b) b.SetBlendWeight(clip1Weight);
    }
    #endregion

    #region ====================================== 相位锁（仅 Walk/Run 双路时启用） ======================================

    public void EnablePhaseLockForWalkRun(float? initPhase01 = null)
    {
        if (currentNode is BlendAnimationNode b && b.ClipCount == 2)
        {
            b.EnablePhaseLock(initPhase01);
        }
    }


    public void UpdatePhaseLockForWalkRun(float walkWeight)
    {
        if (currentNode is BlendAnimationNode b && b.ClipCount == 2)
        {
            b.UpdatePhaseLock(walkWeight);
        }
    }

    public void DisablePhaseLockForWalkRun(float speed0 = 1f, float speed1 = 1f)
    {
        if (currentNode is BlendAnimationNode b && b.ClipCount == 2)
        {
            b.DisablePhaseLock(speed0, speed1);
        }
    }
    #endregion

    #region ====================================== RootMotion 回调（可选） ======================================
    private Action<Vector3, Quaternion> rootMotionAction;
    private void OnAnimatorMove()
    {
        rootMotionAction?.Invoke(animator.deltaPosition, animator.deltaRotation);
    }
    public void SetRootMotionAction(Action<Vector3, Quaternion> rootMotionAction)
    {
        this.rootMotionAction = rootMotionAction;
    }
    public void ClearRootMotionAction()
    {
        rootMotionAction = null;
    }
    #endregion

}
