using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
using ECM2;

/// <summary>
/// Player_MoveState
/// -------------------------------------------------------------------------
/// 职责：
/// - 处理玩家在“移动状态”下的输入、动画和位移逻辑。
/// - 键鼠：支持左Ctrl 切换走路/跑步模式。
/// - 手柄：根据摇杆幅度自动决定 Walk/Run 权重，并在阈值下平滑过渡。
/// - 动画：调用 Player_Controller 播放 Walk/Run 的 Blend 动画，并启用相位锁，保证步频同步。
/// - 移动：通过 CharacterController.Move 实现位移；方向取决于主相机朝向。
/// - 旋转：只旋转模型部分，不影响整体 Player 节点（避免破坏相机逻辑）。
///
/// 主要结构：
/// - Init：初始化 CharacterController、输入系统、配置参数。
/// - Enter：启用输入，切入 Walk/Run Blend 动画，并启用相位锁。
/// - Update：根据输入更新状态、计算 Blend 权重、推进相位锁，最后处理移动和旋转。
/// - Exit：关闭相位锁，停用输入。
///
/// 注意事项：
/// - CharacterController 必须挂在 Player 上，否则无法移动。
/// - 主相机需要标记为 MainCamera，否则方向计算会报错。
/// - 重力值目前是写死的 -9.8f * deltaTime，可根据需要改成累积速度。
/// -------------------------------------------------------------------------
/// </summary>

/// <summary>
/// 玩家移动状态
/// </summary>
public class Player_MoveState : PlayerStateBase
{
    private Character ecmCharacter;   // 角色控制器（ECM2）
    private InputControls input;      // 生成的输入类
    private InputAction moveAction;   // player/Move
    private SHSariaConfig shSariaConfig;  //把配置文件里的数值（walkSpeed）包一层属性来取
    private float walkSpeed => shSariaConfig != null ? shSariaConfig.walkSpeed : 1f; //walkSpeed只读，外部无法随意修改，如果没填就取1
    private float walkHold => shSariaConfig != null ? shSariaConfig.walkHold : 0.5f;
    private float runSpeed => shSariaConfig != null ? shSariaConfig.runSpeed : 3f;
    private float rotateSpeed => shSariaConfig != null ? shSariaConfig.rotateSpeed : 10f;

    // 键鼠：是否强制走路（左Ctrl 切换）
    private bool forceWalkKeyboard = false;

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);    //调用基类的init，如果不写这个player就不会被赋值，后面就会NullReference

        ecmCharacter = player.GetComponent<Character>();
        if (ecmCharacter == null)
        {
            Debug.LogError("[MoveState] 请在 Player 上添加 ECM2.Character 组件。");
        }
        shSariaConfig = player.ShSariaConfig;   // 从宿主拿配置
        input = new InputControls();
        //取出 input 里 "player" 这个 ActionMap 下的 "Move" 动作，并缓存到 moveAction。
        //这个Move是指input系统里设置的Move动作，不是状态机的Move状态
        moveAction = input.player.Move;
    }

    public override void Enter()
    {
        //如果 input != null，就调用 Enable()；
        //如果 input == null，什么也不做，不会抛 NullReferenceException。
        // Enable() 是 Unity 新输入系统里的方法，用来激活所有在 InputControls 里定义的输入动作。
        // 没有调用 Enable() 的话，moveAction.ReadValue<Vector2>() 永远读不到输入。
        input?.Enable();

        // 播放移动的 Blend（这里假设你在 Player_Controller 提供了字符串版封装：
        // PlayBlendAnimation("Walk","Run")，并且 SetBlendWeight(...) 设置到 [Walk, Run] 两路）
        player.PlayBlendAnimation("Walk", "Run");
        // 初始：键鼠默认跑步（Run=1），手柄则会在 Update 里根据摇杆设置
        player.SetBlendWeight(0f);

        // ★ 启用归一化相位锁（不改速度，但步频对齐）
        // 不传初始相位则以通道0当前相位为基准；也可以传 0f 强制从“左脚着地起点”开始
        player.EnableBlendPhaseLock();
    }

    public override void Update()
    {
        // —— 键鼠“强制走路”切换：左Ctrl 按下时在 Walk↔Run 间切换 ——
        // 注意：这里只影响“键鼠方案”；手柄方案用摇杆幅度自动混合。
        if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
        {
            forceWalkKeyboard = !forceWalkKeyboard;
        }

        //检测玩家的输入
        Vector2 move = moveAction.ReadValue<Vector2>();
        float h = move.x;
        float v = move.y;

        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
        {
            //把 ECM2 的方向清零，避免残留速度
            ecmCharacter?.SetMovementDirection(Vector3.zero);
            //切换状态
            player.ChangeState(PlayerState.Idle);
            return;
        }

        //获取相机的y轴旋转值
        //记得要给主相机打上MainCamera的Tag
        if (Camera.main == null)
        {
            Debug.LogError("[Player_MoveState] 场景中没有带 MainCamera 标签的相机！");
            return; // 提前结束，避免后面继续访问 null
        }
        float cameraRotY = Camera.main.transform.localEulerAngles.y;

        Vector3 inputDir = new Vector3(h, 0f, v);
        //把输入向量 inputDir 按照相机的 Y 轴朝向旋转一遍。
        //让四元数和向量相乘，表示这个向量按照这个四元数进行旋转之后获得的新向量
        Vector3 worldDir = Quaternion.Euler(0, cameraRotY, 0) * inputDir;
        Vector3 desiredDir = worldDir.normalized;

        // ========= 核心：手柄阈值 + 平滑混合规则 =========
        // 判断是否使用了手柄：优先使用“当前这个 Action 的活动控制器设备”来判断，
        // 如果拿不到，再退化为 Gamepad.current 的启用帧判断。
        bool usingGamepad =
            (moveAction.activeControl != null && moveAction.activeControl.device is Gamepad)
            || (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame);

        float walkWeight;
        float targetSpeed; // 期望物理速度（在 walkSpeed ~ runSpeed 之间）

        if (usingGamepad)
        {
            // 摇杆幅度（0..1）
            float mag = Mathf.Clamp01(move.magnitude);

            if (mag > 0f && mag < walkHold)
            {
                // 小幅推动：全额 Walk
                walkWeight = 1f;
                targetSpeed = walkSpeed;
            }
            else
            {
                // 进入混合区：从 walkHold → 1 映射到 0 → 1 的跑步权重
                float t = Mathf.InverseLerp(walkHold, 1f, mag); // 线性 0..1
                                                                // 用 SmoothStep 平滑一下过渡手感（比线性更顺）
                float runWeight = Mathf.SmoothStep(0f, 1f, t);
                walkWeight = 1f - runWeight;

                // 速度也随之从 walkSpeed → runSpeed 插值
                targetSpeed = Mathf.Lerp(walkSpeed, runSpeed, runWeight);
            }
        }
        else
        {
            // 键鼠：LeftCtrl 强制走路，否则跑
            if (forceWalkKeyboard)
            {
                walkWeight = 1f;
                targetSpeed = walkSpeed;
            }
            else
            {
                walkWeight = 0f;
                targetSpeed = runSpeed;
            }
        }

        // —— 把“方向 × 速度比例”交给 ECM2 —— 
        // 注意：请让 ECM2 的 Max Speed ≈ runSpeed；否则比例会失真
        float speedFactor = targetSpeed / Mathf.Max(0.0001f, runSpeed);
        ecmCharacter?.SetMovementDirection(desiredDir * speedFactor);

        // —— 动画权重与相位锁 —— 
        player.SetBlendWeight(walkWeight);
        player.UpdateBlendPhaseLock(walkWeight);

        // —— 旋转：只转模型（保持原手感）——
        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            player.ModelTransform.rotation = Quaternion.Slerp(
                player.ModelTransform.rotation,
                Quaternion.LookRotation(desiredDir),
                Time.deltaTime * rotateSpeed
            );
        }
    }


    //离开当前状态时停止监听输入
    public override void Exit()
    {
        // 关闭相位锁并恢复两轨自动播放（可按需自定义速度）
        player.DisableBlendPhaseLock(1f, 1f);
        input?.Disable();
    }
}
