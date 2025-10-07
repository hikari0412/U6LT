using UnityEngine;

/// <summary>
/// 存储当前帧角色的物理状态快照，供各个状态读取。
/// </summary>
public struct MotionSnapshot
{
    // === 速度与方向 ===
    public Vector3 speedWorld;      // 世界空间速度
    public Vector3 speedLocal;      // 角色本地速度
    public float speedXZ;      // 水平速度（忽略y）
    public float speedY;   // 垂直速度（y分量）
    public float speedRadio;         // 0..1 速度比例（根据最大速度归一化）

    // === 地面状态 ===
    public bool isGrounded;       // 是否在地面
    public bool isFalling;        // 是否处于下落中
    public float slopeAngleDeg;   // 当前地面坡度

    // === 输入方向与动作意图 ===
    public Vector3 wishDirLocal;  // 本地输入方向（去掉y并归一化）
    public bool runHeld;          // 是否按住奔跑键
    public bool jumpBottonDown;         // 本帧是否按下跳跃键

    // === 帧事件（瞬时触发）===
    public bool justJumped;       // 这帧是否刚起跳
    public bool justLanded;       // 这帧是否刚落地

    // （可扩展字段）
    public bool preLand;          // 即将落地（用于预判落地动画）
}