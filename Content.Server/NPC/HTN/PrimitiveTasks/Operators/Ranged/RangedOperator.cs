using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Shared.MobState;
using Content.Shared.MobState.Components;
using Robust.Shared.Audio;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Ranged;

public sealed class RangedOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [ViewVariables, DataField("targetKey", required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Minimum damage state that the target has to be in for us to consider attacking.
    /// </summary>
    [ViewVariables, DataField("targetState")]
    public DamageState TargetState = DamageState.Alive;

    // Like movement we add a component and pass it off to the dedicated system.

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        // Don't attack if they're already as wounded as we want them.
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target))
        {
            return (false, null);
        }

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState != null &&
            mobState.CurrentState > TargetState)
        {
            return (false, null);
        }

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var ranged = _entManager.EnsureComponent<NPCRangedCombatComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
        ranged.Target = blackboard.GetValue<EntityUid>(TargetKey);

        if (blackboard.TryGetValue<float>(NPCBlackboard.RotateSpeed, out var rotSpeed))
        {
            ranged.RotationSpeed = new Angle(rotSpeed);
        }

        if (blackboard.TryGetValue<SoundSpecifier>("SoundTargetInLOS", out var losSound))
        {
            ranged.SoundTargetInLOS = losSound;
        }
    }

    public override void Shutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.Shutdown(blackboard, status);
        _entManager.RemoveComponent<NPCRangedCombatComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var status = HTNOperatorStatus.Continuing;

        if (_entManager.TryGetComponent<NPCRangedCombatComponent>(owner, out var combat))
        {
            // Success
            if (_entManager.TryGetComponent<MobStateComponent>(combat.Target, out var mobState) &&
                mobState.CurrentState != null &&
                mobState.CurrentState > TargetState)
            {
                status = HTNOperatorStatus.Finished;
            }
            else
            {
                switch (combat.Status)
                {
                    case CombatStatus.TargetUnreachable:
                    case CombatStatus.NotInSight:
                        status = HTNOperatorStatus.Failed;
                        break;
                    case CombatStatus.Normal:
                        status = HTNOperatorStatus.Continuing;
                        break;
                    default:
                        status = HTNOperatorStatus.Failed;
                        break;
                }
            }
        }

        if (status != HTNOperatorStatus.Continuing)
        {
            _entManager.RemoveComponent<NPCRangedCombatComponent>(owner);
        }

        return status;
    }
}