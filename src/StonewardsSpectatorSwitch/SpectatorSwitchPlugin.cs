using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace StonewardsSpectatorSwitch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SpectatorSwitchPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.thereisnouser.stonewards.spectatorswitch";
        public const string PluginName = "Stonewards Spectator Switch";
        public const string PluginVersion = "0.1.0";

        private readonly List<FirstPersonController> _alivePlayers = new List<FirstPersonController>(4);
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _orbitDistance;
        private ConfigEntry<float> _eyeHeight;
        private ConfigEntry<bool> _autoAdvance;
        private ConfigEntry<bool> _showButtons;
        private SpectatorHud _hud;

        private bool _cursorBorrowed;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;

        private FirstPersonController _target;
        private FirstPersonController _lastLocalPlayer;
        private Camera _renderCamera;
        private Camera _overriddenCamera;
        private Vector3 _originalPosition;
        private bool _dead;
        private bool _wasDead;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Allow left/right mouse clicks to cycle through living teammates while dead.");
            _autoAdvance = Config.Bind("General", "AutoAdvanceWhenTargetDies", true,
                "Automatically select another living teammate if the current spectator target dies or leaves.");
            // First release uses the existing game-controlled view angle by default.
            // The old internal chase-camera offsets are intentionally retired.
            _orbitDistance = Config.Bind("Camera", "OrbitDistance", 0f,
                new ConfigDescription("Optional pullback along your current view direction (0 = player eye position).",
                    new AcceptableValueRange<float>(0f, 8f)));
            _eyeHeight = Config.Bind("Camera", "EyeHeight", 1.55f,
                new ConfigDescription("Camera eye height relative to the selected player's transform.",
                    new AcceptableValueRange<float>(0.5f, 3f)));

            _showButtons = Config.Bind("UI", "ShowButtons", true,
                "Show clickable spectator buttons in the HUD while dead.");
            _hud = new SpectatorHud(CycleFromButton);

            Logger.LogInfo("Spectator Switch v" + PluginVersion + " initialized (client-side). LMB: next, RMB: previous; hold ALT to click spectator buttons.");
        }

        private void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void Update()
        {
            if (_enabled == null || !_enabled.Value)
            {
                ResetSpectator();
                return;
            }

            var manager = GameManager.Instance;
            var local = manager != null ? manager.LocalPlayer : null;
            _dead = local != null && local.PlayerStats != null && local.PlayerStats.isDead;

            // The same plugin instance can outlive a lobby/run. Never retain stale players or camera state.
            if (local != _lastLocalPlayer)
            {
                ResetSpectator();
                _lastLocalPlayer = local;
                _dead = local != null && local.PlayerStats != null && local.PlayerStats.isDead;
            }

            if (!_dead)
            {
                if (_wasDead || _target != null)
                    ResetSpectator();
                _wasDead = false;
                return;
            }

            _wasDead = true;
            RefreshPlayers();
            if (_target != null && !_alivePlayers.Contains(_target))
            {
                _target = null;
                if (_autoAdvance.Value)
                    Cycle(1);
            }

            // Never show the spectator picker over inventory, pause or class menus.
            bool deadInput = InputManager.Instance != null &&
                InputManager.Instance.CurrentInputState == InputManager.InputState.Dead;
            bool shown = false;
            if (deadInput && _showButtons.Value && _hud != null)
                shown = _hud.Show(_target != null ? _target.playerName : null,
                    _alivePlayers.IndexOf(_target), _alivePlayers.Count);
            else
                _hud?.Hide();

            // The game can lock the mouse for camera look. Holding ALT temporarily
            // releases it so UI Toolkit buttons can receive real pointer clicks.
            // On release, restore the game's cursor settings.
            UpdateCursorForButtons(deadInput && shown);

            if (!deadInput || Mouse.current == null)
                return;

            // A free cursor belongs to the on-screen buttons. Do not also execute
            // the global LMB shortcut, which would turn a previous-click into next.
            if (Cursor.visible && Cursor.lockState == CursorLockMode.None)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                Cycle(1);
            else if (Mouse.current.rightButton.wasPressedThisFrame)
                Cycle(-1);
        }

        private void CycleFromButton(int direction)
        {
            if (_enabled == null || !_enabled.Value || !_dead ||
                InputManager.Instance == null ||
                InputManager.Instance.CurrentInputState != InputManager.InputState.Dead)
                return;

            Cycle(direction);
        }

        private void UpdateCursorForButtons(bool canClick)
        {
            var keyboard = Keyboard.current;
            bool altHeld = keyboard != null &&
                (keyboard[Key.LeftAlt].isPressed || keyboard[Key.RightAlt].isPressed);

            if (!canClick || !altHeld)
            {
                ReleaseCursorOverride();
                return;
            }

            if (!_cursorBorrowed)
            {
                _savedCursorLock = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                _cursorBorrowed = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursorOverride()
        {
            if (!_cursorBorrowed)
                return;

            _cursorBorrowed = false;
            // If a menu has since taken control of the cursor, leave it alone.
            if (InputManager.Instance == null ||
                InputManager.Instance.CurrentInputState != InputManager.InputState.Dead)
                return;

            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = _savedCursorLock;
                Cursor.visible = _savedCursorVisible;
            }
        }

        private void RefreshPlayers()
        {
            _alivePlayers.Clear();
            var players = FirstPersonController.LocalPlayers;
            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && !p.isLocalPlayer && p.PlayerStats != null &&
                    !p.PlayerStats.isDead && p.gameObject.activeInHierarchy)
                    _alivePlayers.Add(p);
            }

            // netId is stable for an individual connection, unlike scene hierarchy ordering.
            _alivePlayers.Sort((a, b) => a.netId.CompareTo(b.netId));
        }

        private void Cycle(int direction)
        {
            RefreshPlayers();
            if (_alivePlayers.Count == 0)
            {
                _target = null;
                Logger.LogInfo("Spectator: no living teammates to watch.");
                return;
            }

            int current = _alivePlayers.IndexOf(_target);
            int next = current < 0
                ? (direction > 0 ? 0 : _alivePlayers.Count - 1)
                : (current + direction + _alivePlayers.Count) % _alivePlayers.Count;
            _target = _alivePlayers[next];
            _renderCamera = null; // Reacquire after vanilla death/scene camera changes.
            Logger.LogInfo("Spectating " + _target.playerName + " (" + (next + 1) + "/" + _alivePlayers.Count + ").");
        }

        private void OnCameraPreCull(Camera camera) => TryOverrideCamera(camera);
        private void OnCameraPostRender(Camera camera) => RestoreCamera(camera);
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) => TryOverrideCamera(camera);
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera) => RestoreCamera(camera);

        // IMPORTANT: only override render-time camera pose. Do not teleport players,
        // edit network state, or change the local player's own camera parent/rotation.
        private void TryOverrideCamera(Camera camera)
        {
            if (!_dead || _target == null || camera == null || !camera.enabled ||
                _enabled == null || !_enabled.Value || _overriddenCamera != null)
                return;

            if (camera.cameraType != CameraType.Game || camera.targetTexture != null)
                return;

            if (_renderCamera == null || !_renderCamera.isActiveAndEnabled)
                _renderCamera = FindMainGameCamera();
            if (camera != _renderCamera)
                return;

            var player = _target;
            if (player == null || player.PlayerStats == null || player.PlayerStats.isDead)
                return;

            var t = player.transform;
            Vector3 eye = t.position + Vector3.up * _eyeHeight.Value;
            // Keep the native camera rotation unchanged. Mouse look continues to
            // belong to the game; only the selected teammate's position changes.
            // Optional orbit distance uses the viewer's look direction, not
            // the selected player's forward vector.
            Vector3 desired = eye - camera.transform.forward * _orbitDistance.Value;

            // Only perform collision correction when the user deliberately enables
            // the optional third-person pullback.
            if (_orbitDistance.Value > 0.05f)
            {
                RaycastHit hit;
                if (Physics.Linecast(eye, desired, out hit, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider != null && !hit.collider.transform.IsChildOf(t))
                {
                    Vector3 awayFromWall = (eye - desired).normalized;
                    desired = hit.point + awayFromWall * 0.2f;
                }
            }

            _originalPosition = camera.transform.position;
            _overriddenCamera = camera;
            camera.transform.position = desired;
        }

        private static Camera FindMainGameCamera()
        {
            var main = Camera.main;
            if (main != null && main.isActiveAndEnabled && main.targetTexture == null)
                return main;

            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam != null && cam.isActiveAndEnabled &&
                    cam.cameraType == CameraType.Game && cam.targetTexture == null &&
                    cam.name.IndexOf("main", StringComparison.OrdinalIgnoreCase) >= 0)
                    return cam;
            }
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam != null && cam.isActiveAndEnabled &&
                    cam.cameraType == CameraType.Game && cam.targetTexture == null)
                    return cam;
            }
            return null;
        }

        private void RestoreCamera(Camera camera)
        {
            if (camera == null || camera != _overriddenCamera)
                return;
            camera.transform.position = _originalPosition;
            _overriddenCamera = null;
        }

        private void ResetSpectator()
        {
            ReleaseCursorOverride();
            _hud?.Hide();
            if (_overriddenCamera != null)
                RestoreCamera(_overriddenCamera);
            _target = null;
            _renderCamera = null;
            _dead = false;
            _wasDead = false;
            _alivePlayers.Clear();
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            ResetSpectator();
        }

        private void OnDestroy()
        {
            ResetSpectator();
            _hud?.Dispose();
        }
    }
}
