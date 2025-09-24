using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JKFrame;
using UnityEditor.ShaderGraph;
using UnityEngine;

/// <summary>
/// 坚城塞雷娅配置文件
/// </summary>
[CreateAssetMenu(fileName = "SHSariaConfig", menuName = "Config/SHSariaConfig")]

public class SHSariaConfig : ConfigBase
{
    public float walkSpeed;
    public float rotateSpeed;
    
    public Dictionary<string, AnimationClip> StandAnimationDic;

    public AnimationClip GetAnimationByName(string animationName)
    {
        return StandAnimationDic[animationName];
    }

}