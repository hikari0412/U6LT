using UnityEngine;
using System.Collections;
using System;
using Unity.VisualScripting;
using UnityEngine.PlayerLoop;

public class FootIK : MonoBehaviour
{
    private Animator animator;

    // 用于测试的 IK 开关
    [Tooltip("用于测试的 IK 开关")]
    [SerializeField]
    private bool useIK = true;

    // 是否启用 IK 的旋转（角度）修正
    [Tooltip("是否启用 IK 的旋转（角度）修正")]
    [SerializeField]
    private bool useIKRot = true;

    // 作为地面的图层（Layer）
    [Tooltip("作为地面的图层（Layer）")]
    [SerializeField]
    private LayerMask fieldLayer;

    // 右脚的权重（内部参数，不在 Inspector 显示）
    private float rightFootWeight = 1f;
    // 左脚的权重（内部参数，不在 Inspector 显示）
    private float leftFootWeight = 1f;

    // 右脚的位置（内部参数）
    private Vector3 rightFootIKPosition;
    // 左脚的位置（内部参数）
    private Vector3 leftFootIKPosition;
    // 右脚的旋转（内部参数）
    private Quaternion rightFootRot;
    // 左脚的旋转（内部参数）
    private Quaternion leftFootRot;

    // 右脚与左脚的距离（未使用的变量，占位）（内部参数）
    private float distance;

    // 落脚位置的偏移量
    [Tooltip("落脚位置的偏移量")]
    [SerializeField]
    private Vector3 offset = new Vector3(0f, 0.06f, 0f);

    // 碰撞体（Collider）的中心位置（内部参数）
    private Vector3 defaultCenter;

    // 射线长度
    [Tooltip("射线长度")]
    [SerializeField]
    private float rayRange = 1f;

    // 射线起点的偏移
    [Tooltip("射线起点的偏移")]
    [SerializeField]
    private Vector3 rayPositionOffset = Vector3.up * 0.3f;

    // 是否修改身体重心
    [Tooltip("是否修改身体重心")]
    [SerializeField]
    private bool isChangeBodyPosition = true;

    // 调整身体重心时的速度（旧方案参数）
    [Tooltip("调整身体重心时的速度")]
    [SerializeField]
    private float bodyPositionSpeed = 50f;

    // 上一帧的身体重心位置（内部参数）
    private Vector3 preBodyPosition;

    // 脚部的射线是否命中地面（内部参数）
    private bool rightFootGrounded;
    private bool leftFootGrounded;

    // 平滑所需的速度缓存（内部参数）
    private float rightWVel, leftWVel;
    private Vector3 rPosVel, lPosVel;

    // 开关平滑
    [Tooltip("打开用的平滑时长")]
    [SerializeField] float bodyBlendEnableTime = 0.08f;  // 打开用的平滑时长

    [Tooltip("关闭回收时长")]
    [SerializeField] float bodyBlendDisableTime = 0.12f;  // 关闭回收时长

    // 偏移平滑
    [Tooltip("偏移自身的低通")]
    [SerializeField] float bodyOffsetSmoothTime = 0.06f;  // 偏移自身的低通

    [Tooltip("每秒最大改变量（米/秒）")]
    [SerializeField] float bodyOffsetMaxSpeed = 1.0f;   // 每秒最大改变量（米/秒）

    // 安全限制
    [Tooltip("允许最大下沉距离")]
    [SerializeField] float bodyOffsetDownClamp = 0.20f;  // 允许最大下沉 20 cm

    [Tooltip("允许最大上抬距离")]
    [SerializeField] float bodyOffsetUpClamp = 0.0f;  // 一般不允许上抬

    // 内部平滑缓存（不在 Inspector 显示）
    float _bodyBlend, _bodyBlendVel;  // 开关→权重 的平滑
    float _offsetY, _offsetYVel;     // 目标偏移Y的平滑

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void OnAnimatorIK()
    {
        // 不使用 IK 的情况下，后续不做任何处理
        if (!useIK)
        {
            return;
        }

        //获取动画曲线
        rightFootWeight = animator.GetFloat("RightFootWeight");
        leftFootWeight = animator.GetFloat("LeftFootWeight");
        isChangeBodyPosition = animator.GetBool("isChangeBodyPosition");

        // 右脚IK射线的可视化
        Debug.DrawRay(animator.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, Vector3.down * rayRange, Color.red);
        // 发射右脚IK的检测射线
        var ray = new Ray(animator.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, Vector3.down);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayRange, fieldLayer))
        {
            rightFootGrounded = true;
            rightFootIKPosition = hit.point;

            // 设置右脚 IK
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootIKPosition + offset);
            if (useIKRot)
            {
                rightFootRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rightFootWeight);
                animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootRot);
            }
        }
        else
        {
            rightFootGrounded = false;
        }

        // 发射左脚的检测射线
        ray = new Ray(animator.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, Vector3.down);
        // 左脚射线的可视化
        Debug.DrawRay(animator.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, Vector3.down * rayRange, Color.red);

        if (Physics.Raycast(ray, out hit, rayRange, fieldLayer))
        {
            leftFootGrounded = true;
            leftFootIKPosition = hit.point;

            // 设置左脚 IK
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootIKPosition + offset);

            if (useIKRot)
            {
                leftFootRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
                animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootRot);
            }
        }
        else
        {
            leftFootGrounded = false;
        }
        // 当需要调整身体重心时（旧方案示例，已注释保留）
        // if (isChangeBodyPosition && rightFootGrounded && leftFootGrounded)
        // {
        //     var rightFootDistance = rightFootIKPosition.y - transform.position.y;
        //     var leftFootDistance = leftFootIKPosition.y - transform.position.y;
        //     var distance = rightFootDistance < leftFootDistance ? rightFootDistance : leftFootDistance;
        //     var nowBodyPosition = animator.bodyPosition + Vector3.up * distance;
        //     animator.bodyPosition = Vector3.Lerp(preBodyPosition, nowBodyPosition, bodyPositionSpeed * Time.deltaTime);
        //     preBodyPosition = animator.bodyPosition;
        // }

        // —— 计算“目标偏移” ——
        float targetOffsetY = 0f;
        if (rightFootGrounded && leftFootGrounded)
        {
            var rightFootDistance = rightFootIKPosition.y - transform.position.y;
            var leftFootDistance = leftFootIKPosition.y - transform.position.y;
            targetOffsetY = Mathf.Min(rightFootDistance, leftFootDistance);
        }
        // 限幅，避免地形剧烈变化带来的猛跳
        targetOffsetY = Mathf.Clamp(targetOffsetY, -bodyOffsetDownClamp, bodyOffsetUpClamp);

        // —— 平滑“启用权重”（把 bool 变成 0↔1 的曲线） ——
        bool switchOn = animator.GetBool("isChangeBodyPosition");
        float targetBlend = switchOn ? 1f : 0f;
        float blendTime = switchOn ? bodyBlendEnableTime : bodyBlendDisableTime;
        _bodyBlend = Mathf.SmoothDamp(_bodyBlend, targetBlend, ref _bodyBlendVel, blendTime);

        // —— 平滑“偏移量本身”，再乘以开关权重 ——
        _offsetY = Mathf.SmoothDamp(_offsetY, targetOffsetY, ref _offsetYVel, bodyOffsetSmoothTime, bodyOffsetMaxSpeed);

        // 最终偏移（开关关闭时会自动衰减到 0）
        float finalOffsetY = _offsetY * _bodyBlend;

        // —— 只做相对位移叠加（无状态），不会与上一帧产生断层 ——
        Vector3 basePos = animator.bodyPosition;             // 当前动画给出的自然位置
        animator.bodyPosition = basePos + Vector3.up * finalOffsetY;
    }

}
