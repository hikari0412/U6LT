using UnityEngine;
using System.Collections;
using System;
using Unity.VisualScripting;
using UnityEngine.Video;

public class FootIKController : MonoBehaviour
{
    private Animator animator;

    private Vector3 leftFootIK, rightFootIK;
    private Vector3 leftFootPos, rightFootPos;
    private Quaternion leftFootRot, rightFootRot;
    private float leftFootWeight, rightFootWeight = 0f;

    [SerializeField] private LayerMask iKLayer;
    [SerializeField][Range(0, 0.2f)] private float rayHitOffset;
    [SerializeField] private float rayCastDistance;

    [SerializeField] private bool enableIK;
    [SerializeField] private float iKSphereRadius = 0.05f;
    [SerializeField] private float posSphereRadius = 0.05f;

    private void Awake()
    {
        animator = this.gameObject.GetComponent<Animator>();
        leftFootIK = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        rightFootIK = animator.GetIKPosition(AvatarIKGoal.RightFoot);
    } 

    private void OnAnimatorIK(int layerIndex)
    {
        leftFootIK = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        rightFootIK = animator.GetIKPosition(AvatarIKGoal.RightFoot);

        //目前这两行获取不到
        leftFootWeight = animator.GetFloat("LeftFootWeight");
        rightFootWeight = animator.GetFloat("RightFootWeight");

        if (!enableIK) {return; }

        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);

        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);

        animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootPos);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootRot);

        animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootPos);
        animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootRot);
    }

    private void FixedUpdate() 
    {
        Debug.DrawLine(leftFootIK + (Vector3.up * 0.5f), leftFootIK + Vector3.down * rayCastDistance, Color.blue, Time.deltaTime);
        Debug.DrawLine(rightFootIK + (Vector3.up * 0.5f), rightFootIK + Vector3.down * rayCastDistance, Color.blue, Time.deltaTime);

        if(Physics.Raycast(leftFootIK + (Vector3.up * 0.5f),Vector3.down, out RaycastHit hitL, rayCastDistance + 1, iKLayer))
        {
            Debug.DrawRay(hitL.point, hitL.normal, Color.red, Time.deltaTime);

            leftFootPos = hitL.point + Vector3.up * rayHitOffset;
            leftFootRot = Quaternion.FromToRotation(Vector3.up, hitL.normal) * transform.rotation;
        }

        if(Physics.Raycast(rightFootIK + (Vector3.up * 0.5f),Vector3.down, out RaycastHit hitR, rayCastDistance + 1, iKLayer))
        {
            Debug.DrawRay(hitR.point, hitR.normal, Color.red, Time.deltaTime);

            rightFootPos = hitR.point + Vector3.up * rayHitOffset;
            rightFootRot = Quaternion.FromToRotation(Vector3.up, hitR.normal) * transform.rotation;
        }
    }

    private void OnDrawGizmos() 
    {
        Gizmos.color = Color.green;//IK位置绿色
        Gizmos.DrawSphere(leftFootIK, iKSphereRadius);
        Gizmos.DrawSphere(rightFootIK, iKSphereRadius);
        Gizmos.color = Color.cyan;//脚位置青色
        Gizmos.DrawSphere(leftFootPos, posSphereRadius);
        Gizmos.DrawSphere(rightFootPos, posSphereRadius);
    }
}
