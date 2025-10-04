using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;

public class Player_JumpState : PlayerStateBase
{
    private Character ecmCharacter;
    private InputControls input;
    private InputAction moveAction;
    private InputAction jumpAction;

    private SHSariaConfig shSariaConfig;

    private bool leftGroundOnce;          // 跳跃期间是否离地过
    private float landTimer;
    private bool landingStarted;     // 进入落地阶段的一次性门闩
    private float landHoldTime = 0.1f;

    private string currentAnim;

    private float stateTime;
    [SerializeField] private float jumpStartMinTime = 0.06f;

    [SerializeField] private float groundStableTime = 0.08f;
    private float groundedTimer;


    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);

        ecmCharacter = player.GetComponent<Character>();
        if (ecmCharacter == null)
        {
            Debug.LogError("[JumpState] 缺少 ECM2.Character 组件。");
        }
        shSariaConfig = player.ShSariaConfig;
        input = new InputControls();
        moveAction = input.player.Move;
        jumpAction = input.player.Jump;
    }

    public override void Enter()
    {
        input?.Enable();

        leftGroundOnce = false;
        landingStarted = false;
        landTimer = 0f;
        stateTime = 0f;
        currentAnim = null;

        // 起跳动画
        player.PlayAnimation("JumpStart");
    }

    public override void Update()
    {
        stateTime += Time.deltaTime;

        // 读取 ECM2 状态
        bool grounded = ecmCharacter.IsGrounded();
        Vector3 vel = ecmCharacter.GetVelocity();
        float speed = new Vector2(vel.x, vel.z).magnitude;

        // 记录离地
        if (!grounded)
        {
            leftGroundOnce = true;
            if (stateTime >= jumpStartMinTime)
            { PlayOnce("JumpLoop"); }
            // 一旦离开地面，确保落地阶段标志复位
            landingStarted = false;
            landTimer = 0f;
            return;
        }

        groundedTimer = grounded ? (groundedTimer + Time.deltaTime) : 0f;
        //&& groundedTimer >= groundStableTime

        // 落地阶段：必须离地过 + 已接地 + 正在下降（或速度非上升）
        if (grounded && leftGroundOnce  &&  groundedTimer >= groundStableTime && vel.y <= 0f)
        {
            // 只在“进入落地阶段的第一帧”触发一次落地动画
            if (!landingStarted)
            {
                landingStarted = true;
                landTimer = 0f;
                PlayOnce("JumpLand");
            }

            // 计时，给落地动画一个展示时间
            landTimer += Time.deltaTime;
            if (landTimer >= landHoldTime)
            {
                Vector2 move = moveAction.ReadValue<Vector2>();
                float h = move.x;
                float v = move.y;
                // 用真实速度决定去 Idle 还是 Move
                if (h == 0 && v == 0)
                {
                    // 消除残留输入方向
                    ecmCharacter.SetMovementDirection(Vector3.zero);

                    player.ChangeState(PlayerState.Idle);
                    return; // 记得 return
                }
                else if (h != 0 || v != 0)
                {
                    player.ChangeState(PlayerState.Move);
                    return; // 也要 return
                }
            }
            return; // 处于落地过渡期间，本帧不再执行其他逻辑
        }
    }

    private void PlayOnce(string clipName)
    {
        if (currentAnim == clipName) return;
        currentAnim = clipName;
        player.PlayAnimation(clipName); // 如果支持，改用 CrossFade 版本更顺滑
    }

    public override void Exit()
    {
        landingStarted = false;
        leftGroundOnce = false;
        input?.Disable();
    }
}
