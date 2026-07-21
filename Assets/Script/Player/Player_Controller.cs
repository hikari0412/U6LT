using UnityEngine;
using JKFrame;
using UnityEngine.InputSystem;
using ECM2;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Collections;
using System;

public class Player_Controller : SingletonMono<Player_Controller>, IStateMachineOwner
{
    [SerializeField] Animation_Contorller animation_Contorller;
    [SerializeField] private SHSariaConfig shSariaConfig;
    public SHSariaConfig ShSariaConfig => shSariaConfig;// 方便外部访问配置
    private float walkSpeedRadio => shSariaConfig != null ? shSariaConfig.walkSpeedRadio : 0.5f; //walkSpeed只读，外部无法随意修改，如果没填就取1
    private float walkHold => shSariaConfig != null ? shSariaConfig.walkHold : 0.5f;


    // 你的运动快照（供所有状态读取）
    public MotionSnapshot CurrentMotion { get; private set; }

    // ---------------------------
    // 用于update采集玩家意图的参数
    // ---------------------------
    private Vector3 _wishDirWorld = Vector3.zero;//意图移动的方向
    private float _speedRatio = 0f;//速度比例
    private bool _lastMoveWasKeyboard = true;//上一次的移动方式是否为键盘（用于判断是否切换）
    private bool _jumpPressedFlag = false;// 本帧是否按下了跳跃（供快照使用，写完即清）
    bool _jumpConsumed;        // 跳跃意图是否已消费（避免多次）
    private float _jumpBufferTimer = 0f; //跳跃土狼时间计时器（用于判断是否触发跳跃）
    private const float JUMP_BUFFER_TIME = 0.12f;//跳跃土狼时间（即离开地面一小段时间内仍可触发跳跃动作）   
    private bool _prevGrounded = false;// 上一物理步是否在地面（用于落地沿边缘检测）

    // —— 阈值（迟滞，防抖）——
    const float START_MOVE = 0.06f;  // Idle -> Move
    const float STOP_MOVE = 0.03f;  // Move -> Idle

    [SerializeField] private Transform modelTransform;//把模型部分拖进来，以防旋转等影响player controller
    public Transform ModelTransform => modelTransform;
    private StateMachine stateMachine;// JKFrame 的状态机实例
    private PlayerState currnetPlayerState; // 玩家的当前状态标识


    // ---------------------------
    // 摄像机控制参数
    // ---------------------------

    [Header("Camera Settings")]

    // 成员变量（可序列化引用或在 Awake/Start 里 GetComponent）
    [SerializeField] private CinemachineCamera freeLookCam;
    [Tooltip("缩放灵敏度（滚轮倍率）")]
    [SerializeField] private float zoomSensitivity = 1.0f;

    [Tooltip("缩放灵敏度（手柄缩放速度）")]
    [SerializeField] private float gamepadZoomSpeed = 40f;

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
    private InputAction _moveSwitchAction;
    private InputAction _zoomInAction;
    private InputAction _zoomOutAction;
    private InputAction _dashAction;
    private bool _walkToggle = false;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private string dashAnimationName = "Dash";
    [SerializeField] private float dashAnimationSpeed = 1f;
    [SerializeField] private float dashTransitionFixedTime = 0.1f;

    private bool _isDashing = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private Vector3 _dashDirection = Vector3.zero;
    private bool _dashAnimationWarningLogged = false;

    public bool IsDashing => _isDashing;
    public Vector3 DashDirection => _dashDirection;
    public bool HasDashAnimation =>
        !string.IsNullOrEmpty(dashAnimationName) &&
        shSariaConfig != null &&
        shSariaConfig.StandAnimationDic != null &&
        shSariaConfig.StandAnimationDic.ContainsKey(dashAnimationName);


    /// <summary>
    /// 读取ECM2的物理状态等，记入MotionSnapshot
    /// </summary>
    private void BuildSnapshotFromECM2()
    {
        // 读取 ECM2 的真实速度
        Vector3 v = ecmcharacter.GetVelocity();

        // —— 填充快照 —— 
        MotionSnapshot motionSS = CurrentMotion; // 在原有基础上更新，保留上一帧的帧事件等

        // 速度与方向
        motionSS.speedWorld = v;
        motionSS.speedLocal = transform.InverseTransformDirection(v);
        motionSS.speedY = v.y;

        Vector3 vXZ = v; vXZ.y = 0f;
        motionSS.speedXZ = vXZ.magnitude;

        // 速度比例（0..1，仅看水平速度）
        float maxSpd = Mathf.Max(0.0001f, ecmcharacter.GetMaxSpeed());
        motionSS.speedRadio = Mathf.InverseLerp(0f, maxSpd, motionSS.speedXZ);

        // 地面/下落
        motionSS.isGrounded = ecmcharacter.IsGrounded();
        motionSS.isFalling = ecmcharacter.IsFalling();

        //坡度角（暂时搁置）

        // 本地输入方向（来自 ECM2 的 MovementDirection；去Y并归一）
        Vector3 wishWorld = ecmcharacter.GetMovementDirection();
        Vector3 wishLocal = transform.InverseTransformDirection(wishWorld);
        wishLocal.y = 0f;
        if (wishLocal.sqrMagnitude > 1e-6f) wishLocal.Normalize();
        motionSS.wishDirLocal = wishLocal;

        // 推断是否处于“跑档”（相对你配置的 walkSpeedRadio 给个小裕量）
        motionSS.runHeld = motionSS.speedRadio > (walkSpeedRadio + 0.05f);

        // 本帧是否按下跳跃（来自输入事件的一次性标记）
        motionSS.jumpBottonDown = _jumpPressedFlag;
        motionSS.isDashing = _isDashing;

        // 帧事件：起跳/落地
        // - 起跳：优先由输入事件标记
        motionSS.justJumped = motionSS.justJumped || _jumpPressedFlag;
        // - 落地：上一帧不在地面，这一帧在地面
        motionSS.justLanded = !_prevGrounded && motionSS.isGrounded;

        // === 计时：AirHold & LandHold ===
        float dt = Time.deltaTime;  // 或 Time.fixedDeltaTime，看你在哪调用
        if (!motionSS.isGrounded)
        {
            // 在空中：累计离地时间，清空落地计时
            motionSS.airHoldTime += dt;
            motionSS.landHoldTime = 0f;
        }
        else
        {
            // 在地面：累计落地时间，清空离地计时
            motionSS.landHoldTime += dt;
            motionSS.airHoldTime = 0f;
        }

        // 预落地（可选：留给你后续用射线/球体预测后写入）
        motionSS.preLand = motionSS.preLand && !motionSS.isGrounded; // 例如：着地后自动清

        // 写回、清一次性标记
        CurrentMotion = motionSS;
        _jumpPressedFlag = false;
        _prevGrounded = motionSS.isGrounded;
    }

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
        _moveSwitchAction = _input.player.MoveSwitch;   // Button
        _zoomInAction = _input.player.ZoomInGamePad;   // 按住 LB+RT
        _zoomOutAction = _input.player.ZoomOutGamePad; // 按住 LB+LT
        _dashAction = _input.player.Dash; // Button
    }

    private void OnEnable()
    {
        _input.Enable();

        ecmcharacter.CharacterMovementUpdated += OnCharacterMovementUpdated;

        // 跳跃：按下=Jump，松开=StopJumping（支持可变跳高）
        _jumpAction.performed += OnJumpPerformed;
        _jumpAction.canceled += OnJumpCanceled;

        _zoomInAction.Enable();
        _zoomOutAction.Enable();

        _moveSwitchAction.performed += OnMoveSwitch;
        _dashAction.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        ecmcharacter.CharacterMovementUpdated -= OnCharacterMovementUpdated;

        _jumpAction.performed -= OnJumpPerformed;
        _jumpAction.canceled -= OnJumpCanceled;

        _moveSwitchAction.performed -= OnMoveSwitch;
        _dashAction.performed -= OnDashPerformed;

        _zoomInAction.Disable();
        _zoomOutAction.Disable();

        StopDash();

        _input.Disable();
    }

    private void Start()
    {
        Init();
        stateMachine = ResSystem.GetOrNew<StateMachine>();
        // 初始化并进入默认 Idle（JKFrame 文档推荐做法）
        stateMachine.Init<Player_IdleState>(this);
    }

    /// <summary>
    /// 初始化
    /// </summary>
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
    /// update中仅采集玩家输入的意图，不做物理运算
    /// </summary>
    private void Update()
    {
        // =========================1) 移动意图（相机朝向；键盘可切换 Walk/Run）=========================
        // 读取玩家输入
        Vector2 move = _moveAction.ReadValue<Vector2>();
        float mag = Mathf.Clamp01(move.magnitude);

        // 记录最近输入设备（仅在有输入时更新）
        var activeDev = _moveAction.activeControl != null ? _moveAction.activeControl.device : null;
        if (activeDev != null && mag > 0f)
            _lastMoveWasKeyboard = activeDev is Keyboard;

        // 相机平面方向
        Transform camT = freeLookCam != null ? freeLookCam.transform :
                         (Camera.main != null ? Camera.main.transform : transform);

        Vector3 camForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;

        // 期望方向（世界，y=0）
        Vector3 wishDir = (camForward * move.y + camRight * move.x);
        if (wishDir.sqrMagnitude > 0f) wishDir.Normalize();
        _wishDirWorld = wishDir;

        // 速度占比（键盘：Walk/Run 开关；手柄：摇杆幅度混合）
        float speedRatio = 0f;
        if (_lastMoveWasKeyboard)
        {
            // 键盘：用当前切换状态
            if (mag > 0f)
                speedRatio = _walkToggle ? walkSpeedRadio : 1f;
        }
        else
        {
            // 手柄：按摇杆幅度线性混合
            if (mag > 0f)
            {
                if (mag <= walkHold)
                    speedRatio = walkSpeedRadio;
                else
                {
                    float t = Mathf.InverseLerp(walkHold, 1f, mag);
                    speedRatio = Mathf.Lerp(walkSpeedRadio, 1f, t);
                }
            }
        }
        _speedRatio = speedRatio;

        // =========================2) 缩放（Mouse Scroll / InputActions ZoomIn & ZoomOut）=========================
        // 
        // 
        if (freeLookCam != null)
        {
            float newFOV = freeLookCam.Lens.FieldOfView;

            // ① 鼠标滚轮（仍然支持）
            Vector2 zoom2D = _zoomAction.ReadValue<Vector2>();
            float zoomY = zoom2D.y;
            if (Mathf.Abs(zoomY) > 0.001f)
                newFOV -= zoomY * zoomSensitivity;

            // ② 手柄组合键（ZoomIn / ZoomOut 持续触发）
            bool zoomInPressed = _zoomInAction.IsPressed();
            bool zoomOutPressed = _zoomOutAction.IsPressed();

            float delta = gamepadZoomSpeed * zoomSensitivity * Time.deltaTime;

            if (zoomInPressed)   // 按住 ZoomIn（LB+RT）
                newFOV -= delta;
            if (zoomOutPressed)  // 按住 ZoomOut（LB+LT）
                newFOV += delta;

            // ③ 限制范围并应用
            newFOV = Mathf.Clamp(newFOV, minFOV, maxFOV);
            freeLookCam.Lens.FieldOfView = newFOV;
        }

        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - Time.deltaTime);
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                StopDash();
            }
        }

    }

    /// <summary>
    /// 把update中采集到的玩家意图传给ECM2并做物理运算
    /// </summary>
    private void FixedUpdate()
    {
        //把移动传给ECM2
        Vector3 movementDirection = _isDashing ? Vector3.zero : _wishDirWorld * _speedRatio;
        ecmcharacter.SetMovementDirection(movementDirection);
    }

    private void OnCharacterMovementUpdated(float deltaTime)
    {
        BuildSnapshotFromECM2();

        if (_isDashing && !CurrentMotion.isGrounded)
        {
            StopDash();
        }

        var next = DecideState(CurrentMotion);
        if (next != currnetPlayerState)
        {
            ChangeState(next);
        }
    }

    /// <summary>
    /// 判断角色的ECM2物理状态并切换动画的State
    /// </summary>
    private PlayerState DecideState(MotionSnapshot motionSS)
    {
        // 不在地面
        if (!motionSS.isGrounded)
            return PlayerState.Air;

        if (motionSS.isDashing)
            return PlayerState.Dash;

        // 在地面
        switch (currnetPlayerState)
        {
            case PlayerState.Idle:
                if (motionSS.speedXZ >= START_MOVE) return PlayerState.Move;
                break;

            case PlayerState.Move:
                if (motionSS.speedXZ <= STOP_MOVE) return PlayerState.Idle;
                break;

            case PlayerState.Air:
                // 保险：从空中回地面
                return (motionSS.speedXZ >= START_MOVE) ? PlayerState.Move : PlayerState.Idle;

            case PlayerState.Dash:
                return (motionSS.speedXZ >= START_MOVE) ? PlayerState.Move : PlayerState.Idle;
        }

        // 灰区：保持当前，确保所有路径都有返回
        return currnetPlayerState;
    }

    // ----------------------------------------------------------------
    // 输入事件：跳跃 
    // ----------------------------------------------------------------

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        // 起跳前短暂停用贴地约束，避免“粘地”抵消垂直速度（可按项目需要开/关）
        ecmcharacter.PauseGroundConstraint(0.12f);

        ecmcharacter.useRootMotion = true;
        Debug.Log("useRootMotin:" + ecmcharacter.useRootMotion.ToString());

        // 触发 ECM2 的跳跃输入（ECM2 内部会在模拟阶段 DoJump）
        ecmcharacter.Jump();

        //记录本帧“按下跳跃”
        _jumpPressedFlag = true;
        ecmcharacter.useRootMotion = false;
        Debug.Log("useRootMotin:" + ecmcharacter.useRootMotion.ToString());

        DoAfter(0.5f, () => ecmcharacter.useRootMotion = false);
    }


    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        // 松开 → 可变跳高（更短的上升）
        ecmcharacter.StopJumping();
        _jumpPressedFlag = false;
    }

    private void OnDashPerformed(InputAction.CallbackContext ctx)
    {
        TryStartDash();
    }

    // ----------------------------------------------------------------
    // 输入事件：键盘走跑切换
    // ----------------------------------------------------------------
    private void OnMoveSwitch(InputAction.CallbackContext ctx)
    {
        _walkToggle = !_walkToggle;
    }

    private void TryStartDash()
    {
        if (!CanDash()) return;

        Vector3 dashDir = _wishDirWorld;
        if (dashDir.sqrMagnitude < 0.0001f)
        {
            dashDir = modelTransform != null ? modelTransform.forward : transform.forward;
        }

        dashDir.y = 0f;
        if (dashDir.sqrMagnitude < 0.0001f) return;

        StartDash(dashDir.normalized);
    }

    private bool CanDash()
    {
        if (_isDashing) return false;
        if (_dashCooldownTimer > 0f) return false;
        if (ecmcharacter == null) return false;
        if (!ecmcharacter.IsGrounded()) return false;
        return true;
    }

    private void StartDash(Vector3 dashDir)
    {
        _isDashing = true;
        _dashDirection = dashDir;
        _dashTimer = Mathf.Max(0.01f, dashDuration);
        _dashCooldownTimer = Mathf.Max(0f, dashCooldown);

        var snapshot = CurrentMotion;
        snapshot.isDashing = true;
        CurrentMotion = snapshot;

        float dashSpeed = dashDistance / _dashTimer;
        Vector3 dashVelocity = dashDir * dashSpeed;
        ecmcharacter.LaunchCharacter(dashVelocity, false, true);

        ChangeState(PlayerState.Dash);
    }

    private void StopDash()
    {
        if (!_isDashing) return;

        _isDashing = false;
        _dashTimer = 0f;
        _dashDirection = Vector3.zero;

        var snapshot = CurrentMotion;
        snapshot.isDashing = false;
        CurrentMotion = snapshot;
    }

    public void PlayDashAnimation()
    {
        if (!HasDashAnimation)
        {
            if (!_dashAnimationWarningLogged)
            {
                Debug.LogWarning("[Player_Controller] 未配置 Dash 动画，将仅应用位移效果。");
                _dashAnimationWarningLogged = true;
            }
            return;
        }

        if (animation_Contorller == null)
        {
            Debug.LogWarning("[Player_Controller] animation_Contorller 未配置，无法播放 Dash 动画。");
            return;
        }

        if (shSariaConfig.StandAnimationDic.TryGetValue(dashAnimationName, out AnimationClip clip))
        {
            animation_Contorller.PlayDashAnimation(clip, dashAnimationSpeed, dashTransitionFixedTime);
        }
    }

    /// <summary>
    /// 修改状态标识
    /// </summary>
    /// <param name="playerState"></param>
    public void ChangeState(PlayerState playerState)
    {
        this.currnetPlayerState = playerState;
        switch (playerState)
        {
            case PlayerState.Idle:
                stateMachine.ChangeState<Player_IdleState>();
                break;
            case PlayerState.Move:
                stateMachine.ChangeState<Player_MoveState>();
                break;
            case PlayerState.Air:
                stateMachine.ChangeState<Player_AirState>();
                break;
            case PlayerState.Dash:
                stateMachine.ChangeState<Player_DashState>();
                break;
            default:
                break;
        }
    }

    //通用延迟函数
    private void DoAfter(float delay, Action action)
    {
        StartCoroutine(DoAfterCoroutine(delay, action));
    }

    private IEnumerator DoAfterCoroutine(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
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


    /// <summary>
    /// 播放blend动画（多个）
    /// </summary>
    public void PlayBlendAnimation(List<AnimationClip> clips, float speed = 1, float transitionFixedTime = 0.25f)
    {
        if (clips == null || clips.Count == 0)
        {
            Debug.LogWarning("[Player_Controller] clips 为空，无法播放动画。");
            return;
        }

        // 过滤掉空元素
        List<AnimationClip> validClips = clips.FindAll(c => c != null);

        if (validClips.Count == 0)
        {
            Debug.LogWarning("[Player_Controller] 所有 AnimationClip 都为空。");
            return;
        }

        // 传入 controller，内部自己创建 mixer 输入数量 = validClips.Count
        animation_Contorller.PlayBlendAnimation(validClips, speed, transitionFixedTime);

    }

    /// <summary>
    /// 设置blend动画的权重（多个）
    /// </summary>
    /// <param name="clip1Weight"></param>
    public void SetBlendWeight(List<float> weightList)
    {
        animation_Contorller.SetBlendWeight(weightList);
    }


    /// <summary>启用 Walk/Run 的相位锁（可选初相位）。</summary>
    public void EnableBlendPhaseLock(float? initPhase01 = null) => animation_Contorller.EnablePhaseLockForWalkRun(initPhase01);

    /// <summary>按当前 Walk 权重推进相位（每帧调用）。</summary>
    public void UpdateBlendPhaseLock(float walkWeight) => animation_Contorller.UpdatePhaseLockForWalkRun(walkWeight);

    /// <summary>关闭相位锁，恢复自动播放速度（默认1,1；如需自定义可传参）。</summary>
    public void DisableBlendPhaseLock(float s0 = 1f, float s1 = 1f) => animation_Contorller.DisablePhaseLockForWalkRun(s0, s1);
}
