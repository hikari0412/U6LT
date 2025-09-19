using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 玩家待机状态
/// </summary>
public class Player_IdleState : PlayerStateBase
{
    public override void Enter()
    {
        //播放待机动作
        player.PlayAnimation("Idle");
    }
}