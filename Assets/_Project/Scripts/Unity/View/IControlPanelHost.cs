// Assets/_Project/Scripts/Unity/View/IControlPanelHost.cs
using System.Collections.Generic;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// What the controls panel may read and what it may ask for. The
    /// composition root implements it.
    /// </summary>
    /// <remarks>
    /// <b>The panel owns no state.</b> The selection (operator, ability, target,
    /// cell) lives in MatchBootstrap, because the board click handler and the
    /// highlight layer need it too. The panel renders a snapshot and reports
    /// clicks as intents, the same shape as props and callbacks in a web UI.
    ///
    /// Both panels, OnGUI and uGUI, go through these intents while they
    /// coexist (ADR-0008 consequence 6). A button therefore does the same
    /// thing in either panel, and the stranger test compares layouts, not two
    /// implementations of the rules around a click.
    /// </remarks>
    public interface IControlPanelHost
    {
        MatchFactory.Match Match { get; }
        /// <summary>How the squads on the table were chosen, as setup names it (DR2).</summary>
        string SquadSummary { get; }

        /// <summary>Whether the seat to play is a CPU's (BOT2). Every intent is ignored while it is.</summary>
        bool CpuTurn { get; }

        /// <summary>"CPU · BRAWLER" for a CPU seat, null for a human one (BOT3).</summary>
        string SeatTag(PlayerColor seat);
        IReadOnlyList<string> Log { get; }

        OperatorState SelectedOperator { get; }
        AbilityDefinition SelectedAbility { get; }
        OperatorState SelectedTarget { get; }
        CellRef? SelectedCell { get; }

        /// <summary>True when the selected ability has everything it needs to be cast.</summary>
        bool CastReady { get; }

        /// <summary>
        /// Who the selected ability may be aimed at: the engine's legal list,
        /// minus the caster. Empty when nothing is selected.
        /// </summary>
        IReadOnlyList<OperatorState> CastTargets();

        void Roll();
        void EndTurn();
        void Deploy(OperatorState op);

        /// <summary>Spends the whole roll when <paramref name="dieFace"/> is null, one die otherwise.</summary>
        void Move(OperatorState op, int? dieFace);

        /// <summary>
        /// Selects an operator to command (move or cast), or clears the
        /// selection if it was already selected. The board and the panel both
        /// select through this.
        /// </summary>
        void ToggleOperator(OperatorState op);

        /// <summary>Selects the ability if the engine says it is ready, or clears it if it was selected.</summary>
        void ToggleAbility(AbilityDefinition ability);

        void ToggleTarget(OperatorState op);

        /// <summary>Casts the selected ability. Does nothing unless its target or cell is chosen.</summary>
        void Cast();

        /// <summary>Starts a new match with the next seed.</summary>
        void Reseed();
    }
}
