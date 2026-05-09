using UnityEngine;

public class IdleState : State
{
    public IdleState(Character character, StateMachine stateMachine) 
        : base(character, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (attackAction.triggered)
        {
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}