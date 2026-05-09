using UnityEngine;
using UnityEngine.InputSystem;

public class State
{
    protected Character character;
    protected StateMachine stateMachine;

    protected Vector3 gravityVelocity;
    protected Vector3 velocity;
    protected Vector2 input;

    protected InputAction attackAction;

    public State(Character _character, StateMachine _stateMachine)
    {
        character = _character;
        stateMachine = _stateMachine;

        attackAction = character.playerInput.actions["Attack"];
    }

    public virtual void Enter()
    {
        // Intentionally quiet by default (Console spam hurts editor FPS).
    }

    public virtual void HandleInput()
    {

    }

    public virtual void LogicUpdate()
    {

    }

    public virtual void PhysicsUpdate()
    {

    }

    public virtual void Exit()
    {

    }
}