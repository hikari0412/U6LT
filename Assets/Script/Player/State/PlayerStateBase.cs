using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JKFrame;

/// <summary>
/// 玩家状态的基类
/// </summary>
/// PlayerStateBase 的作用是：
///作为所有玩家状态的父类，继承 StateBase。
///在初始化时，把传进来的宿主 owner 强制转成 Player_Controller，存到 player 字段里。
///这样具体状态类（Idle、Move、Attack）就不用再关心宿主是谁，直接用 player 就能控制玩家。
public abstract class PlayerStateBase : StateBase
{
    protected Player_Controller player;

    //因为 Player_Controller 实现了 IStateMachineOwner 接口，
    //所以它可以被接口引用（向上转型），
    //也可以在需要时转回具体类型（向下转型）
    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);
        //把owner强制转成 Player_Controller
        player = (Player_Controller)owner;
    }
}
