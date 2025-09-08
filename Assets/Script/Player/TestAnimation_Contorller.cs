using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Runtime.Serialization;
using Sirenix.OdinInspector;

public class TestAnimation_Contorller : MonoBehaviour
{
    [SerializeField] AnimationClip animationClip1;
    [SerializeField] AnimationClip animationClip2;
    [SerializeField] Animation_Contorller animation_controller;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            animation_controller.PlayAnimation(animationClip1);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            animation_controller.PlayAnimation(animationClip2);
        }
    }
}
