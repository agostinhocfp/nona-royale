// Assets/_Project/Scripts/Unity/View/PresentationQueue.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>The named moments the queue announces (MOTION.md decision 2). Stage 5's audio listens for these.</summary>
    public enum PresentationBeat
    {
        /// <summary>The dice start to tumble.</summary>
        DiceRolled,
        /// <summary>The dice show the engine's faces.</summary>
        DiceLanded,
        /// <summary>Pieces start walking their forward moves.</summary>
        Walk,
        /// <summary>Damage, healing, evasion or absorption shows on a piece.</summary>
        Hit,
        /// <summary>An operator is knocked out.</summary>
        Knockout,
        /// <summary>The board and the HUD catch up with the engine.</summary>
        Settle,
    }

    /// <summary>
    /// Plays the presentation of each batch one step after another: dice,
    /// then walks, then hits, then knockouts, then the settle (MOTION.md
    /// increment MO1).
    /// </summary>
    /// <remarks>
    /// <b>It sequences; it decides nothing.</b> The composition root turns a
    /// batch of engine events into steps and enqueues them. A step is an
    /// action to start, an optional condition that says it has finished, and
    /// a minimum hold. Steps that finish at once chain in the same frame.
    ///
    /// <b><see cref="IsBusy"/> is the gate.</b> Human input and the CPU
    /// driver wait on it, so one action reads before the next begins.
    ///
    /// <b>Scaled time</b>, so pause freezes the queue with the rest of the
    /// board (MOTION.md rules). <see cref="Speed"/> multiplies it, for the
    /// CPU hurry key.
    ///
    /// <b>Essential steps always run.</b> <see cref="Flush"/> skips the
    /// cosmetic steps and runs the essential ones (the settle, which feeds the
    /// history and the end screen) at once. An instant batch flushes before it
    /// is handled, so nothing it follows is lost. <see cref="Clear"/> drops
    /// everything, for a teardown.
    ///
    /// <b>No step can hold the game hostage.</b> A step whose condition is
    /// still false after <see cref="MaxStepSeconds"/> is let go.
    /// </remarks>
    public sealed class PresentationQueue : MonoBehaviour
    {
        /// <summary>The longest any step may keep the queue busy, in scaled seconds.</summary>
        public const float MaxStepSeconds = 6f;

        /// <summary>Raised as each step starts, with the step's beat.</summary>
        public event Action<PresentationBeat> BeatStarted;

        /// <summary>Multiplier on the queue's clock. 1 is normal.</summary>
        public float Speed { get; set; } = 1f;

        private sealed class Step
        {
            public PresentationBeat Beat;
            public Action Start;
            public Func<bool> Done;
            public float Hold;
            public bool Essential;
        }

        private readonly Queue<Step> _pending = new Queue<Step>();
        private Step _current;
        private float _elapsed;

        /// <summary>Whether a step is playing or waiting to play.</summary>
        public bool IsBusy => _current != null || _pending.Count > 0;

        /// <summary>
        /// Adds a step to the end of the queue.
        /// </summary>
        /// <param name="beat">The beat announced when the step starts.</param>
        /// <param name="start">What the step does. Runs once.</param>
        /// <param name="done">True once the step has finished. Null means "after the hold".</param>
        /// <param name="hold">The least time the step keeps the queue busy, in scaled seconds.</param>
        /// <param name="essential">Runs even when the queue is flushed.</param>
        public void Enqueue(PresentationBeat beat, Action start, Func<bool> done = null, float hold = 0f,
            bool essential = false)
        {
            _pending.Enqueue(new Step
            {
                Beat = beat,
                Start = start,
                Done = done,
                Hold = Mathf.Max(0f, hold),
                Essential = essential,
            });

            // A queue with nothing playing starts at once, so a step that
            // finishes immediately has run before the caller's next line.
            if (_current == null) Advance(0f);
        }

        /// <summary>Ends the current step and runs every pending essential step now, skipping the rest.</summary>
        public void Flush()
        {
            _current = null;

            while (_pending.Count > 0)
            {
                var step = _pending.Dequeue();
                if (step.Essential) Run(step);
            }
        }

        /// <summary>Drops everything without running it.</summary>
        public void Clear()
        {
            _current = null;
            _pending.Clear();
        }

        private void Update() => Advance(Time.deltaTime * Mathf.Max(0f, Speed));

        private void Advance(float delta)
        {
            // Bounded, so a queue fed from inside a step can never spin a frame forever.
            for (int guard = 0; guard < 64; guard++)
            {
                if (_current == null)
                {
                    if (_pending.Count == 0) return;

                    _current = _pending.Dequeue();
                    _elapsed = 0f;
                    Run(_current);

                    // Starting a step can flush or clear the queue.
                    if (_current == null) continue;
                }
                else
                {
                    _elapsed += delta;
                    delta = 0f;
                }

                if (!Finished(_current)) return;
                _current = null;
            }
        }

        private bool Finished(Step step)
        {
            if (_elapsed >= MaxStepSeconds) return true;
            if (_elapsed < step.Hold) return false;
            return step.Done == null || step.Done();
        }

        private void Run(Step step)
        {
            BeatStarted?.Invoke(step.Beat);
            step.Start?.Invoke();
        }
    }
}