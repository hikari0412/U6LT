using UnityEngine;

public class Player_DashState : PlayerStateBase
{
    public override void Enter()
    {
        player.PlayDashAnimation();

        if (player.ModelTransform == null)
        {
            return;
        }

        Vector3 direction = player.DashDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            player.ModelTransform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
