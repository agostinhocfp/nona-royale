// Assets/Tests/EditMode/Engine/TurnKit.cs
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>Shared fixture steps for tests that need to get through a turn.</summary>
    internal static class TurnKit
    {
        /// <summary>
        /// Pays whatever the engine says is still owed this turn: a deploy on a
        /// held 6, a pooled move, a doubles re-roll when nothing can move
        /// (§6.1, §6.2, 2026-09-25). Stops when nothing is owed or nothing it
        /// tries is accepted, so a fixture cannot spin.
        /// </summary>
        public static void PayWhatIsOwed(GameEngine engine)
        {
            for (int guard = 0; guard < 20 && engine.Phase == TurnPhase.Action && engine.MustSpendRoll; guard++)
            {
                if (engine.MustDeploy)
                {
                    var seated = engine.CurrentPlayer.Operators.First(o => o.IsInYard);
                    engine.Execute(new DeployCommand(seated.Id));
                }
                else if (engine.CanMove)
                {
                    bool moved = false;
                    foreach (var op in engine.CurrentPlayer.Operators.Where(o => !o.IsInYard && !engine.IsHome(o)))
                    {
                        if (engine.Execute(new MoveCommand(op.Id)).OfType<CommandRejected>().Any()) continue;
                        moved = true;
                        break;
                    }

                    if (!moved) break;
                }
                else if (engine.MustRollAgain)
                {
                    engine.Execute(new RollDiceCommand());
                }
                else break;
            }
        }

        /// <summary>Pays what is owed, then ends the turn.</summary>
        public static void SpendRollAndEndTurn(GameEngine engine)
        {
            PayWhatIsOwed(engine);
            engine.Execute(new EndTurnCommand());
        }
    }
}
