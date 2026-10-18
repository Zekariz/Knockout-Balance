namespace KnockoutBalance
{
    public interface IPlayerInput
    {
        float MoveInput { get; }
        bool JumpPressed { get; }
        bool JumpReleased { get; }
        bool PunchPressed { get; }
        bool BlockHeld { get; }
        bool DashPressed { get; }
    }
}
