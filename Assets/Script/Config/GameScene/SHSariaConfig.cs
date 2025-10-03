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
    public float walkSpeed;
    [PropertySpace(SpaceAfter = 10)]  

    [InfoBox("摇杆推动幅度小于walkHold时保持100%walk状态", InfoMessageType.Info)]
    public float walkHold;
    [PropertySpace(SpaceAfter = 10)]  

    [InfoBox("Run Speed要与player物体上挂的ECM2 Character中的Max Walk Speed保持一致", InfoMessageType.Info)]
    public float runSpeed;
    [PropertySpace(SpaceAfter = 10)]  

    public float rotateSpeed;
    [PropertySpace(SpaceAfter = 10)]  
    public Dictionary<string, AnimationClip> StandAnimationDic;

    public AnimationClip GetAnimationByName(string animationName)
    {
        return StandAnimationDic[animationName];
    }

}