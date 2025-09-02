using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Runtime.Serialization;
using Sirenix.OdinInspector;

public class Animation_Contorller : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] AnimationClip animationClip1;
    [SerializeField] AnimationClip animationClip2;
    [Range(0, 1), OnValueChanged(nameof(ClipWeightValueChanged))] public float clipWeight;
    private PlayableGraph graph;
    public AnimationMixerPlayable mixer;

    private void Start()
    {
        // 1.创建图
        graph = PlayableGraph.Create("Animation_Contorller");

        // 2.设置图的时间模式
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        // 3.创建2个ClipPlayable，分别包裹一个AnimationClip
        AnimationClipPlayable clipPlayable1 = AnimationClipPlayable.Create(graph, animationClip1);
        AnimationClipPlayable clipPlayable2 = AnimationClipPlayable.Create(graph, animationClip2);

        // 4.创建混合器
        mixer = AnimationMixerPlayable.Create(graph,2);

        // 5.ClipPlayable连接混合器（这里0代表有1个端口，1代表有两个端口）
        graph.Connect(clipPlayable1, 0, mixer, 0);
        graph.Connect(clipPlayable2, 0, mixer, 1);

        // 6.设置默认的动画权重（端口，权重）
        mixer.SetInputWeight(0, 1);
        mixer.SetInputWeight(1, 0);

        // 7.创建Output
        AnimationPlayableOutput playableOutput = AnimationPlayableOutput.Create(graph, "Animation", animator);

        // 8.让混合器链接上OutPut
        playableOutput.SetSourcePlayable(mixer);

        // 9.播放图
        graph.Play();

    }
    private void OnDisable()
    {
		// 7.销毁PlayableGraph
        graph.Destroy();

    }

    void ClipWeightValueChanged()
    {   

        mixer.SetInputWeight(0, clipWeight);
        mixer.SetInputWeight(1, 1 - clipWeight);

    }

}
