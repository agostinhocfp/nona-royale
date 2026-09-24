// Assets/_Project/Scripts/Unity/View/FeedbackLayer.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Plays the one-off effects that explain what just happened: numbers,
    /// misses, blocks, the burst where an operator was neutralized, and the
    /// moments a seat's debt changes hands.
    /// </summary>
    /// <remarks>
    /// <c>COMBAT_SYSTEMS</c> §9.3 says <c>DamageDealt</c>, <c>DamageEvaded</c>
    /// and <c>DamageAbsorbed</c> are separate events precisely because the view
    /// has to play three visibly different things. Until now it played none of
    /// them, so an evaded hit and a landed one looked identical on the board and
    /// the difference lived only in the log.
    /// </remarks>
    public sealed class FeedbackLayer : MonoBehaviour
    {
        private const string BleedCause = "bleed";
        private const string MarkCause = "mark";
        private const string FollowUpCause = "follow-up";

        /// <summary>Kian's Zero-Day charge detonates at upkeep, so it is labelled like bleed.</summary>
        private const string ZeroDayCause = NonaRoyale.Core.Services.DeferredOperatorEffects.ChargeCause;
        private const string CriticalCause = "critical";

        private static Color HitColour => UiTheme.Damage;
        private static Color OverTimeColour => UiTheme.OverTime;
        private static Color HealColour => UiTheme.Heal;
        private static Color EvadeColour => UiTheme.Evade;
        private static Color BlockColour => UiTheme.Block;

        private float _scale = 1f;

        public void Bind(float cellSize) => _scale = cellSize;

        /// <summary>
        /// A damage number, labelled and tinted by what dealt it.
        /// </summary>
        /// <remarks>
        /// Over-time damage is called out because it lands during upkeep, in a
        /// phase where nothing else moves. A bare number there explains nothing —
        /// an operator's health drops, and if that was its last it vanishes to its
        /// yard with no stated cause (<c>PRESENTATION.md</c> §2).
        ///
        /// Everything else is left unlabelled. A hit already has a visible cause:
        /// the piece that just moved into it, or the ability the player watched
        /// being cast.
        /// </remarks>
        public void Damage(Vector3 at, int amount, string cause)
        {
            bool overTime = IsUpkeepCause(cause);

            // A critical is labelled too, though it has a visible agent: the
            // number alone cannot say why Vendetta's second blow hit three
            // times as hard as its first.
            string text = overTime ? $"-{amount} {cause}"
                : cause == CriticalCause ? $"-{amount} CRIT"
                : $"-{amount}";

            FloatingText.Spawn(
                transform, at, text,
                overTime ? OverTimeColour : HitColour,
                cause == CriticalCause ? _scale * 1.25f : _scale);
        }

        /// <summary>
        /// Causes that land at upkeep, where nothing on the board moves to
        /// explain them. Luka's follow-up joins bleed and marks.
        /// </summary>
        private static bool IsUpkeepCause(string cause) =>
            cause == BleedCause || cause == MarkCause || cause == FollowUpCause || cause == ZeroDayCause;

        public void Heal(Vector3 at, int amount) =>
            FloatingText.Spawn(transform, at, $"+{amount}", HealColour, _scale);

        public void Evaded(Vector3 at)
        {
            FloatingText.Spawn(transform, at, "MISS", EvadeColour, _scale);
            Pulse(at, UiTheme.WithAlpha(EvadeColour, 0.8f), 2.6f);
        }

        public void Absorbed(Vector3 at)
        {
            FloatingText.Spawn(transform, at, "BLOCK", BlockColour, _scale);
            Pulse(at, UiTheme.WithAlpha(BlockColour, 0.9f), 2.2f);
        }

        /// <summary>
        /// A hit that arrived on safe ground and was voided (§4.4, third
        /// amendment). "SAFE", not "BLOCK": a block is the target's shield and
        /// is spent; this is the cell, and costs nothing — the player needs to
        /// read that stepping off is what exposes the piece.
        /// </summary>
        public void Sheltered(Vector3 at)
        {
            FloatingText.Spawn(transform, at, "SAFE", UiTheme.Sheltered, _scale);
            Pulse(at, UiTheme.WithAlpha(UiTheme.Sheltered, 0.85f), 2.2f);
        }

        /// <summary>
        /// The burst where an operator fell, labelled with what finished it.
        /// </summary>
        /// <remarks>
        /// Played at the cell it died on, not at the yard it reappears in — the
        /// piece teleports home, and without this there is nothing at all to mark
        /// the most consequential event in the game.
        ///
        /// The cause matters most here for exactly the reason it matters on the
        /// number: a kill at upkeep has no visible agent on the board.
        /// </remarks>
        public void Neutralized(Vector3 at, Color seatColour, string cause)
        {
            bool overTime = IsUpkeepCause(cause);

            FloatingText.Spawn(
                transform, at,
                overTime ? $"DOWN — {cause}" : "DOWN",
                seatColour, _scale * 1.2f);

            Pulse(at, seatColour, 4.2f);
        }

        // ── Debt (COMBAT_SYSTEMS §3.3) ───────────────────────────────────

        /// <summary>
        /// Lifts a debt numeral clear of the damage number the same batch
        /// usually spawns on the same piece, so the two never overprint.
        /// </summary>
        private Vector3 AboveHit(Vector3 at) => at + BoardTilt.ScreenUp * (_scale * 0.55f);

        /// <summary>
        /// A loan landing: "+II" rising off the operator it was aimed at, in
        /// oxblood on the display face. Nothing when the seat was already at
        /// the cap — the history says so, and the board has nothing new to show.
        /// </summary>
        public void DebtIncurred(Vector3 at, int amount)
        {
            if (amount <= 0) return;
            FloatingText.Spawn(transform, AboveHit(at), DebtMark.Loan(amount), UiTheme.Debt, _scale * 1.1f, display: true);
        }

        /// <summary>
        /// Sadist collecting: the figure called in, stamped over the target a
        /// size larger than any damage number, with a blood-velvet ring. The
        /// damage itself still arrives as the usual number beneath it.
        /// </summary>
        public void DebtCalled(Vector3 at, int amount)
        {
            if (amount <= 0) return;
            FloatingText.Spawn(transform, AboveHit(at), DebtMark.Roman(amount), UiTheme.Debt, _scale * 1.6f, display: true);
            Pulse(at, UiTheme.WithAlpha(UiTheme.DebtPlate, 0.95f), 3.4f);
        }

        /// <summary>
        /// A debtor landing on its creditor: the figure, struck through, in
        /// ember over the creditor, with an ember ring.
        /// </summary>
        public void DebtBurned(Vector3 at, int amount)
        {
            if (amount <= 0) return;
            FloatingText.Spawn(transform, AboveHit(at), DebtMark.Burned(amount), UiTheme.DebtBurn, _scale * 1.3f, display: true);
            Pulse(at, UiTheme.WithAlpha(UiTheme.DebtBurn, 0.85f), 3.0f);
        }

        private void Pulse(Vector3 at, Color colour, float finalScale)
        {
            var go = new GameObject("pulse");
            go.transform.SetParent(transform, false);
            go.transform.position = at;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Primitives.Ring;
            renderer.color = colour;
            // Over the pieces' depth band (V1b, FigureTilt).
            renderer.sortingOrder = FigureTilt.FeedbackOrder;

            go.AddComponent<ExpandingPulse>().Begin(_scale * 0.5f, _scale * finalScale);
        }
    }

    /// <summary>An expanding, fading ring. Self-destructing, so nothing tracks it.</summary>
    public sealed class ExpandingPulse : MonoBehaviour
    {
        private const float Lifetime = 0.45f;

        private SpriteRenderer _renderer;
        private float _from;
        private float _to;
        private float _age;

        public void Begin(float from, float to)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _from = from;
            _to = to;
            transform.localScale = Vector3.one * from;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);

            transform.localScale = Vector3.one * Mathf.Lerp(_from, _to, t);

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = 1f - t;
                _renderer.color = colour;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}