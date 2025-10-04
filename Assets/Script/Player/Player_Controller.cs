using UnityEngine;
using JKFrame;
using UnityEngine.InputSystem;
using ECM2;
using Unity.Cinemachine;

public class Player_Controller : SingletonMono<Player_Controller>, IStateMachineOwner
{
    [SerializeField] Animation_Contorller animation_Contorller;
    [SerializeField] private SHSariaConfig shSariaConfig;
    public SHSariaConfig ShSariaConfig => shSariaConfig;// 方便外部访问配置

    [SerializeField] private Transform modelTransform;//把模型部分拖进来，以防旋转等影响player controller
    public Transform ModelTransform => modelTransform;
    private StateMachine stateMachine;
    private PlayerState playerState; // 玩家的当前状态标识

    // ---------------------------
    // 摄像机控制参数
    // ---------------------------

    [Header("Camera Settings")]

    // 成员变量（可序列化引用或在 Awake/Start 里 GetComponent）
    [SerializeField] private CinemachineCamera freeLookCam;
    [Tooltip("缩放灵敏度（滚轮倍率）")]
    [SerializeField] private float zoomSensitivity = 1.0f;

    [Tooltip("最小FOV（值越小视角越近）")]
    [SerializeField] private float minFOV = 10f;

    [Tooltip("最大FOV（值越大视角越远）")]
    [SerializeField] private float maxFOV = 40f;


    // ---------------------------
    // 角色与输入
    // ---------------------------

    protected Character ecmcharacter;    // ECM2 角色

    // 新输入系统的包装类（由 Input System 生成）
    private InputControls _input;

    // 便捷引用（避免每帧查找）
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _zoomAction;
    private InputAction _jumpAction;


    private void Awake()
    {
        // 获取 Character 组件（必须存在 ECM2.Character）
        ecmcharacter = GetComponent<Character>();

        if (ecmcharacter == null)
        {
            Debug.LogError("ThirdPersonController: 未找到 Character 组件！");
            enabled = false;
        }

        // 自动补齐引用，防止忘记拖
        if (animation_Contorller == null)
        {
            animation_Contorller = GetComponentInChildren<Animation_Contorller>();
        }
        // 如果 Animation_Contorller 里需要 Animator，一定要在它的脚本里也做空引用检查

        // 初始化 InputActions（确保你的 InputActions 里有 player.Map 与下列动作）
        _input = new InputControls();

        _moveAction = _input.player.Move;   // Vector2
        _lookAction = _input.player.Look;   // Vector2（建议绑定 Mouse delta / RightStick）
        _zoomAction = _input.player.Zoom;   // float  （建议绑定 Mouse scroll Y）
        _jumpAction = _input.player.Jump;   // Button
    }

    private void OnEnable()
    {
        _input.Enable();

        // 跳跃：按下=Jump，松开=StopJumping（支持可变跳高）
        _jumpAction.performed += OnJumpPerformed;
        _jumpAction.canceled += OnJumpCanceled;
    }

    private void OnDisable()
    {
        _jumpAction.performed -= OnJumpPerformed;
        _jumpAction.canceled -= OnJumpCanceled;

        _input.Disable();
    }

    private void Start()
    {
        Init();
    }
    public void Init()
    {
        // 1) 先初始化动画控制器（若你在 Animation_Contorller 里有 Init 方法的话）
        if (animation_Contorller == null)
        {
            Debug.LogError("[Player_Controller] animation_Contorller 未赋值或未找到组件。请在 Player 上添加/拖拽 Animation_Contorller。");
            return; // 不能继续
        }
        // 如果你的 Animation_Contorller 有 Init()，在这里调用：
        animation_Contorller.Init();

        // 2) 从对象池取状态机
        stateMachine = PoolSystem.GetObject<StateMachine>() ?? new StateMachine();
        stateMachine.Init(this);

        // 3) 进入默认状态
        ChangeState(PlayerState.Idle);

        // 额外：检查配置
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 未赋值。后续 PlayAnimation(\"Idle\") 可能找不到动画。");
        }
    }

    /// <summary>
    /// 修改状态标识
    /// </summary>
    /// <param name="playerState"></param>
    public void ChangeState(PlayerState playerState)
    {
        this.playerState = playerState;
        switch (playerState)
        {
            case PlayerState.Idle:
                stateMachine.ChangeState<Player_IdleState>();
                break;
            case PlayerState.Move:
                stateMachine.ChangeState<Player_MoveState>();
                break;
            case PlayerState.Jump:
                stateMachine.ChangeState<Player_JumpState>();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationClipName"></param>
    public void PlayAnimation(string animationClipName, float speed = 1, bool refreshAnimation = false, float transitionFixedTime = 0.25f)
    {
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 为 null，无法根据名字获取动画。");
            return;
        }

        var clip = shSariaConfig.GetAnimationByName(animationClipName);
        if (clip == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{animationClipName}\" 的 AnimationClip。");
            return;
        }

        animation_Contorller.PlaySingleAnimation(clip, speed, refreshAnimation, transitionFixedTime);
    }

    /// <summary>
    /// 播放blend动画
    /// </summary>
    public void PlayBlendAnimation(string clip1Name, string clip2Name, float speed = 1f, float transitionFixedTime = 0.25f)
    {
        if (shSariaConfig == null)
        {
            Debug.LogWarning("[Player_Controller] shSariaConfig 为 null，无法根据名字获取动画。");
            return;
        }

        AnimationClip clip1 = shSariaConfig.GetAnimationByName(clip1Name);
        AnimationClip clip2 = shSariaConfig.GetAnimationByName(clip2Name);

        if (clip1 == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{clip1Name}\" 的 AnimationClip。");
            return;
        }

        if (clip2 == null)
        {
            Debug.LogWarning($"[Player_Controller] 配置中找不到名为 \"{clip2Name}\" 的 AnimationClip。");
            return;
        }

        animation_Contorller.PlayBlendAnimation(clip1, clip2, speed, transitionFixedTime);
    }

    /// <summary>
    /// 设置blend动画的权重
    /// </summary>
    /// <param name="clip1Weight"></param>
    public void SetBlendWeight(float clip1Weight)
    {
        animation_Contorller.SetBlendWeight(clip1Weight);
    }


    /// <summary>启用 Walk/Run 的相位锁（可选初相位）。</summary>
    public void EnableBlendPhaseLock(float? initPhase01 = null) => animation_Contorller.EnablePhaseLockForWalkRun(initPhase01);

    /// <summary>按当前 Walk 权重推进相位（每帧调用）。</summary>
    public void UpdateBlendPhaseLock(float walkWeight) => animation_Contorller.UpdatePhaseLockForWalkRun(walkWeight);

    /// <summary>关闭相位锁，恢复自动播放速度（默认1,1；如需自定义可传参）。</summary>
    public void DisableBlendPhaseLock(float s0 = 1f, float s1 = 1f) => animation_Contorller.DisablePhaseLockForWalkRun(s0, s1);

    private void Update()
    {
        // =========================
        // 1) 移动（每帧轮询 Vector2）
        // =========================
        Vector2 move2D = _moveAction.ReadValue<Vector2>(); // (-1..1, -1..1)

        // 由输入合成世界空间移动方向
        Vector3 moveDir = new Vector3(move2D.x, 0f, move2D.y);

        // 若角色挂有 cameraTransform，则将移动方向“相对相机”旋转
        if (ecmcharacter.camera)
            moveDir = moveDir.relativeTo(ecmcharacter.cameraTransform, ecmcharacter.GetUpVector());

        // 交给 ECM2（物理加速度、阻尼、地面约束等都由 ECM2 处理）
        ecmcharacter.SetMovementDirection(moveDir);

        // =========================
        // 2) 缩放（Mouse Scroll / Gamepad D-Pad）
        // Zoom 在 InputControls 中是 Vector2（Mouse/scroll 与 Gamepad/dpad），只取 y 分量
        // =========================
        Vector2 zoom2D = _zoomAction.ReadValue<Vector2>();
        float zoomY = zoom2D.y;

        if (Mathf.Abs(zoomY) > 0.0001f && freeLookCam != null)
        {
            float newFOV = Mathf.Clamp(
                freeLookCam.Lens.FieldOfView - zoomY * zoomSensitivity,
                10f, 40f  // 可调
            );
            freeLookCam.Lens.FieldOfView = newFOV;
        }
    }


    // ----------------------------------------------------------------
    // 输入事件：跳跃 
    // ----------------------------------------------------------------

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        // 起跳前短暂停用贴地约束，避免“粘地”抵消垂直速度（可按项目需要开/关）
        ecmcharacter.PauseGroundConstraint(0.12f);

        // 触发 ECM2 的跳跃输入（ECM2 内部会在模拟阶段 DoJump）
        ecmcharacter.Jump();
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        // 松开 → 可变跳高（更短的上升）
        ecmcharacter.StopJumping();
    }

}
