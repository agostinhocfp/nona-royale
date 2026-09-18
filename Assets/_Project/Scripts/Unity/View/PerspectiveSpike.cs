// Assets/_Project/Scripts/Unity/View/PerspectiveSpike.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A throwaway test for the tilted-table look (VISUAL_PASS.md, V0):
    /// does URP's 2D Renderer still light the board when the camera is a
    /// tilted perspective camera? Editor and development builds only.
    /// </summary>
    /// <remarks>
    /// <b>How to use.</b> Enter Play Mode, deal a match, press <b>F9</b>. The
    /// main camera turns into a perspective camera tilted 40° over the board.
    /// <b>F10</b> cycles the tilt (30°, 40°, 48°). F9 again puts the camera
    /// back exactly as it was. Nothing else changes: the pieces still lie
    /// flat, and <b>board clicks are wrong while tilted</b> (they still use
    /// the flat camera's maths). This only answers the lighting question.
    ///
    /// <b>What to look for.</b> The warm pools over the vault, arms and
    /// tables; the cyan glow and bloom on the safe cells; the selection pool
    /// and a cast or knockout flash (LT2). If only the flat global light
    /// remains, 2D point lights don't work with this camera.
    ///
    /// <b>Nothing to wire.</b> It creates itself when Play Mode starts. It
    /// logs what it did once per toggle, with the 2D lights it can see. It
    /// runs after the framing and the camera nudge each frame, so they
    /// can't undo it; when off, it does nothing at all.
    /// </remarks>
    [DefaultExecutionOrder(10000)]
    public sealed class PerspectiveSpike : MonoBehaviour
    {
        private static readonly float[] Pitches = { 30f, 40f, 48f };

        private const float FieldOfView = 30f;

        /// <summary>Half the board's width with the table margin, in world units, at the default spacing.</summary>
        private const float BoardHalf = 8.3f;

        private bool _on;
        private int _pitch = 1;

        private Camera _camera;
        private bool _savedOrtho;
        private float _savedFov;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("perspective_spike (dev)");
            DontDestroyOnLoad(go);
            go.AddComponent<PerspectiveSpike>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Toggle();

            if (_on && Input.GetKeyDown(KeyCode.F10))
            {
                _pitch = (_pitch + 1) % Pitches.Length;
                Debug.Log($"[Spike] Tilt {Pitches[_pitch]}°");
            }
        }

        private void Toggle()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning("[Spike] No main camera.");
                return;
            }

            _on = !_on;

            if (_on)
            {
                _savedOrtho = _camera.orthographic;
                _savedFov = _camera.fieldOfView;
                _savedPosition = _camera.transform.position;
                _savedRotation = _camera.transform.rotation;

                int point = 0, global = 0, enabled = 0;
                foreach (var light in FindObjectsByType<Light2D>())
                {
                    if (light.lightType == Light2D.LightType.Global) global++; else point++;
                    if (light.isActiveAndEnabled) enabled++;
                }

                Debug.Log($"[Spike] Tilted perspective ON at {Pitches[_pitch]}° (F10 cycles). " +
                          $"2D lights: {point} local, {global} global, {enabled} enabled. Board clicks are off-target while tilted.");
            }
            else
            {
                _camera.orthographic = _savedOrtho;
                _camera.fieldOfView = _savedFov;
                _camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
                Debug.Log("[Spike] Tilted perspective OFF; camera restored.");
            }
        }

        private void LateUpdate()
        {
            if (!_on || _camera == null) return;

            float pitch = Pitches[_pitch];
            _camera.orthographic = false;
            _camera.fieldOfView = FieldOfView;

            // The flat camera looks down +z at the board in the XY plane.
            // Tipping it about x makes it look up the board from the near edge.
            var rotation = Quaternion.Euler(-pitch, 0f, 0f);
            var forward = rotation * Vector3.forward;

            // Far enough that the board's width fits the narrower of the two
            // view angles, with a little room; aimed a touch below centre so
            // the near edge, which looks widest, stays on screen.
            float halfV = FieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * Mathf.Max(0.1f, _camera.aspect));
            float distance = BoardHalf * 1.25f / Mathf.Tan(Mathf.Min(halfV, halfH));

            var target = new Vector3(0f, -1.2f, 0f);
            _camera.transform.SetPositionAndRotation(target - forward * distance, rotation);
        }
    }
}
#endif