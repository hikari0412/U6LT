using System.Collections.Generic;
using JKFrame;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 坚城塞雷娅配置文件
/// </summary>
[CreateAssetMenu(fileName = "SHSariaConfig", menuName = "Config/SHSariaConfig")]

public class SHSariaConfig : ConfigBase
{
    [InfoBox("摇杆推动幅度小于walkHold时保持100%walk状态", InfoMessageType.Info)]
    public float walkHold;
    [PropertySpace(SpaceAfter = 10)]

    [InfoBox("walk速度为run速度的多少倍（须小于1），根据不同walk/run动画调整", InfoMessageType.Info)]
    public float walkSpeedRadio;
    [PropertySpace(SpaceAfter = 10)]

    [LabelText("转身速度")]public float rotateSpeed;
    [PropertySpace(SpaceAfter = 10)]

    [LabelText("脚步声资源")]public AudioClip[] FootStepAudioClips;

    [LabelText("标准动作表")]public Dictionary<string, AnimationClip> StandAnimationDic;

    public AnimationClip GetAnimationByName(string animationName)
    {
        return StandAnimationDic[animationName];
    }

}