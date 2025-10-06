using UnityEngine;
using JKFrame;
using UnityEngine.InputSystem;
using ECM2;
using Unity.Cinemachine;
using System.Collections.Generic;

public class Player_Controller : SingletonMono<Player_Controller>, IStateMachineOwner
{
    [SerializeField] Animation_Contorller animation_Contorller;
    [SerializeField] private SHSariaConfig shSariaConfig;
    public SHSariaConfig ShSariaConfig => shSariaConfig;// 方便外部访问配置
    private float walkSpeedRadio => shSariaConfig != null ? shSariaConfig.walkSpeedRadio : 0.5f; //walkSpeed只读，外部无法随意修改，如果没填就取1
    private float walkHold => shSariaConfig != null ? shSariaConfig.walkHold : 0.5f;

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
    private bool _walkToggle = false;
    private bool _lastMoveWasKeyboard = false;

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
    }

    private void OnEnable()
    {
        _input.Enable();

        // 跳跃：按下=Jump，松开=StopJumping（支持可变跳高）
        _jumpAction.performed += OnJumpPerformed;
        _jumpAction.canceled += OnJumpCanceled;

        _zoomInAction.Enable();
        _zoomOutAction.Enable();

        _moveSwitchAction.performed += OnMoveSwitch;
    }

    private void OnDisable()
    {
        _jumpAction.performed -= OnJumpPerformed;
        _jumpAction.canceled -= OnJumpCanceled;

        _moveSwitchAction.performed -= OnMoveSwitch;

        _zoomInAction.Disable();
        _zoomOutAction.Disable();

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

    private void Update()
    {
        // =========================
        // 1) 移动（相机朝向；键盘可切换 Walk/Run）
        // =========================
        Vector2 move = _moveAction.ReadValue<Vector2>();
        float mag = Mathf.Clamp01(move.magnitude);

        // 判断输入设备
        var activeDev = _moveAction.activeControl != null ? _moveAction.activeControl.device : null;
        if (activeDev != null && mag > 0f)
            _lastMoveWasKeyboard = activeDev is Keyboard;

        // 相机方向
        Transform camT = freeLookCam != null ? freeLookCam.transform :
                         (Camera.main != null ? Camera.main.transform : transform);

        Vector3 camForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;

        Vector3 wishDir = (camForward * move.y + camRight * move.x);
        if (wishDir.sqrMagnitude > 0f) wishDir.Normalize();

        // 速度占比
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

        Vector3 movementDirection = wishDir * speedRatio;
        ecmcharacter.SetMovementDirection(movementDirection);

        // =========================
        // 2) 缩放（Mouse Scroll / InputActions ZoomIn & ZoomOut）
        // =========================
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

    // ----------------------------------------------------------------
    // 输入事件：键盘走跑切换
    // ----------------------------------------------------------------
    private void OnMoveSwitch(InputAction.CallbackContext ctx)
    {
        _walkToggle = !_walkToggle;
    }

}
