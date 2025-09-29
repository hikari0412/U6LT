using UnityEngine;
using UnityEngine.InputSystem;
using JKFrame;
//using System.Numerics;

/// <summary>
/// 玩家移动状态
/// </summary>
public class Player_MoveState : PlayerStateBase
{
    private CharacterController characterController;   // 角色控制器（unity提供的组件）
    private InputControls input;      // 生成的输入类
    private InputAction moveAction;   // player/Move
    private SHSariaConfig shSariaConfig;  //把配置文件里的数值（walkSpeed）包一层属性来取
    private float walkSpeed => shSariaConfig != null ? shSariaConfig.walkSpeed : 1f; //walkSpeed只读，外部无法随意修改，如果没填就取1
    private float runSpeed  => shSariaConfig != null ? shSariaConfig.runSpeed  : 1f;
    private float rotateSpeed => shSariaConfig != null ? shSariaConfig.rotateSpeed : 1f;

    // 键鼠：是否强制走路（左Ctrl 切换）
    private bool forceWalkKeyboard = false;

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);    //调用基类的init，如果不写这个player就不会被赋值，后面就会NullReference
        characterController = player.GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("[Player_MoveState] 未找到 CharacterController。");
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
            //切换状态
            player.ChangeState(PlayerState.Idle);
        }
        else
        {
            // =========================
            // TODO1/2：混合 Walk / Run
            // 【注意！】已知问题：走跑的速度不一致，混合时步频不一致
            // 不改速度也能对齐步频：方法就是停下动画自身推进（Speed=0），用统一的“归一化相位”每帧设置两条剪辑的时间。
            // =========================
            float walkWeight = 0f;
            float runWeight  = 1f;

            // 判断是否使用了手柄：优先使用“当前这个 Action 的活动控制器设备”来判断，
            // 如果拿不到，再退化为 Gamepad.current 的启用帧判断。
            bool usingGamepad =
                (moveAction.activeControl != null && moveAction.activeControl.device is Gamepad)
                || (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame);

            if (usingGamepad)
            {
                // 手柄：根据摇杆幅度来混合。
                // 你在 TODO 里写了用 (h+v)/2 当作 walk 的权重，这个会出现负值/超过1的问题。
                // 这里改为更稳定的方案：用幅度 |move| ∈ [0,1] 来表示“跑步程度”，
                // 则 Run 权重 = |move|，Walk 权重 = 1 - |move|。
                float mag = Mathf.Clamp01(move.magnitude);
                runWeight  = mag;
                walkWeight = 1f - mag;
            }
            else
            {
                // 键鼠：默认 Run=1；按左Ctrl 切换为 Walk（再按回去）
                if (forceWalkKeyboard)
                {
                    walkWeight = 1f;
                    runWeight  = 0f;
                }
                else
                {
                    walkWeight = 0f;
                    runWeight  = 1f;
                }
            }

            //（可选）如果你特别想遵循你在 TODO 里写的“用 (h+v)/2 作为 Walk 权重”的规则，
            //可以把上面写好的计算注释掉，启用下面这行：
            //walkWeight = Mathf.Clamp01((Mathf.Abs(h) + Mathf.Abs(v)) * 0.5f); runWeight = 1f - walkWeight;

            // 将权重写给动画（假设顺序为 [Walk, Run]）
            player.SetBlendWeight(walkWeight);

            // =========================
            // 下面处理位移与旋转
            // =========================

            //处理移动
            Vector3 inputDir = new Vector3(h, 0, v);

            //获取相机的y轴旋转值
            //记得要给主相机打上MainCamera的Tag
            if (Camera.main == null)
            {
                Debug.LogError("[Player_MoveState] 场景中没有带 MainCamera 标签的相机！");
                return; // 提前结束，避免后面继续访问 null
            }
            float cameraRotY = Camera.main.transform.localEulerAngles.y;

            //把输入向量 inputDir 按照相机的 Y 轴朝向旋转一遍。
            //让四元数和向量相乘，表示这个向量按照这个四元数进行旋转之后获得的新向量
            Vector3 moveDir = Quaternion.Euler(0, cameraRotY, 0) * inputDir;

            // 根据动画权重选择移动速度：权重与动画一致（Blend 一致性）
            float currSpeed = Mathf.Lerp(walkSpeed, runSpeed, runWeight);

            //玩家的移动量
            Vector3 motion = moveDir * currSpeed * Time.deltaTime;

            //重力值（写死）
            motion.y -= 9.8f * Time.deltaTime;

            //角色控制器移动
            if (characterController != null)
            {
                characterController.Move(motion);
            }

            //处理旋转，旋转只改模型层，不改player（不然会影响相机）
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                player.ModelTransform.rotation = Quaternion.Slerp(
                    player.ModelTransform.rotation,
                    Quaternion.LookRotation(moveDir),
                    Time.deltaTime * rotateSpeed
                );
            }
        }
    }

    //离开当前状态时停止监听输入
    public override void Exit()
    {
        input?.Disable();
    }
}
