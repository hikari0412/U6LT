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
    private float walkSpeed => shSariaConfig != null ? shSariaConfig.walkSpeed : 1f; //walkSpeed只读，外部无法睡意修改，如果没填就取1
    private float rotateSpeed => shSariaConfig != null ? shSariaConfig.rotateSpeed : 1f;
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
        //播放移动动作
        player.PlayAnimation("Move");
    }

    public override void Update()
    {
        //检测玩家的输入
        Vector2 move = moveAction.ReadValue<Vector2>();
        float h = move.x;
        float v = move.y;

        if (h == 0 && v == 0)
        {
            //切换状态
            player.ChangeState(PlayerState.Idle);
        }
        else
        {
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
            //玩家的移动量
            Vector3 motion = moveDir * walkSpeed * Time.deltaTime;
            //重力值（写死）
            motion.y -= 9.8f * Time.deltaTime;
            //角色控制器移动
            characterController.Move(motion);


            //处理旋转，旋转只改模型层，不改player（不然会影响相机）
            player.ModelTransform.rotation = Quaternion.Slerp(player.ModelTransform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * rotateSpeed);
        }
    }

    //离开当前状态时停止监听输入
    public override void Exit()
    {
        input?.Disable();
    }
}
