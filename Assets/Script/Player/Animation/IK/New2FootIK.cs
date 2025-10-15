using UnityEngine;
using System.Collections;
using System;

public class New2FootIK : MonoBehaviour
{
    private Animator animator;

    [SerializeField] private bool useIK = true;
    [SerializeField] private bool useIKRot = true;
    [SerializeField] private LayerMask fieldLayer;

    private float rightFootWeight = 0f;
    private float leftFootWeight  = 0f;

    private Vector3 rightFootIKPosition;
    private Vector3 leftFootIKPosition;
    private Quaternion rightFootRot;
    private Quaternion leftFootRot;

    private float distance;

    [SerializeField] private Vector3 offset = new Vector3(0f, 0.06f, 0f);

    [SerializeField] private float rayRange = 1f;
    [SerializeField] private Vector3 rayPositionOffset = Vector3.up * 0.3f;

    [SerializeField] private bool isChangeBodyPosition = true;
    [SerializeField] private float bodyPositionSpeed = 50f;
    private Vector3 preBodyPosition;
    private bool rightFootGrounded;
    private bool leftFootGrounded;

    [Header("基于原动画脚骨位置的权重计算")]
    [SerializeField] private float penetrationEpsilon = 0.003f;
    [SerializeField] private float fullWeightDistance = 0.03f;
    [SerializeField] private float zeroWeightDistance = 0.12f;

    [Header("身体下沉触发判定（按 IK 位置）")]
    [Tooltip("判定 IK 位置“贴地”的阈值（米），默认 0.02")]
    [SerializeField] private float nearGroundEpsilon = 0.02f;
    [Tooltip("另一只脚 IK 位置与地面允许的最大距离（米），默认 0.08")]
    [SerializeField] private float otherFootMaxDistance = 0.08f;

    void Start()
    {
        animator = GetComponent<Animator>();
        preBodyPosition = animator.bodyPosition;
    }

    void OnAnimatorIK()
    {
        if (!useIK) return;

        float rightGroundY = 0f;
        float leftGroundY  = 0f;

        // —— 右脚 —— //
        Debug.DrawRay(animator.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, Vector3.down * rayRange, Color.red);
        var ray = new Ray(animator.GetIKPosition(AvatarIKGoal.RightFoot) + rayPositionOffset, Vector3.down);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rayRange, fieldLayer))
        {
            rightFootGrounded   = true;
            rightFootIKPosition = hit.point;
            rightGroundY        = hit.point.y;

            Transform rfBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float groundY    = hit.point.y;
            float footY      = rfBone.position.y;

            if (footY < groundY - penetrationEpsilon)
            {
                rightFootWeight = 1f;
            }
            else
            {
                float verticalDist = Mathf.Max(footY - (groundY + offset.y), 0f);
                float t = Mathf.InverseLerp(fullWeightDistance, zeroWeightDistance, verticalDist);
                rightFootWeight = 1f - Mathf.Clamp01(t);
            }

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
            rightFootWeight   = 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFootWeight);
            if (useIKRot) animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rightFootWeight);
        }

        // —— 左脚 —— //
        ray = new Ray(animator.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, Vector3.down);
        Debug.DrawRay(animator.GetIKPosition(AvatarIKGoal.LeftFoot) + rayPositionOffset, Vector3.down * rayRange, Color.red);

        if (Physics.Raycast(ray, out hit, rayRange, fieldLayer))
        {
            leftFootGrounded   = true;
            leftFootIKPosition = hit.point;
            leftGroundY        = hit.point.y;

            Transform lfBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            float groundY    = hit.point.y;
            float footY      = lfBone.position.y;

            if (footY < groundY - penetrationEpsilon)
            {
                leftFootWeight = 1f;
            }
            else
            {
                float verticalDist = Mathf.Max(footY - (groundY + offset.y), 0f);
                float t = Mathf.InverseLerp(fullWeightDistance, zeroWeightDistance, verticalDist);
                leftFootWeight = 1f - Mathf.Clamp01(t);
            }

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
            leftFootWeight   = 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
            if (useIKRot) animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, leftFootWeight);
        }

        // =========================
        // 身体重心下沉（按 IK 位置判断）
        // 条件：isChangeBodyPosition &&
        //   ( 右脚IK贴地 && 左脚IK距地 < otherFootMaxDistance ) ||
        //   ( 左脚IK贴地 && 右脚IK距地 < otherFootMaxDistance )
        // 说明：
        //   IK位置 = hit.point + offset
        //   “贴地”与“距地”均比较 IK位置 与 (groundY + offset.y) 的竖直差
        // =========================
        // 当需要调整身体重心时
        if (isChangeBodyPosition && rightFootGrounded && leftFootGrounded)
        {
            // 计算左右脚与角色脚下位置（transform.position）的高度差
            var rightFootDistance = rightFootIKPosition.y - transform.position.y;
            var leftFootDistance = leftFootIKPosition.y - transform.position.y;
            // 取左右脚中更低的那一侧的高度差
            var distance = rightFootDistance < leftFootDistance ? rightFootDistance : leftFootDistance;
            // 将身体重心下移到较低的那只脚的位置附近
            var nowBodyPosition = animator.bodyPosition + Vector3.up * distance;
            // 做渐变插值；其实也可以像被注释的那行一样一次性设置
            animator.bodyPosition = Vector3.Lerp(preBodyPosition, nowBodyPosition, bodyPositionSpeed * Time.deltaTime);
            preBodyPosition = animator.bodyPosition;
            // animator.bodyPosition = nowBodyPosition; // 一次性设置
        }
    }
}
