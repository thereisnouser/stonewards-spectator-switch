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
        private ConfigEntry<float> _distance;
        private ConfigEntry<float> _height;
        private ConfigEntry<bool> _autoAdvance;

        private FirstPersonController _target;
        private FirstPersonController _lastLocalPlayer;
        private Camera _renderCamera;
        private Camera _overriddenCamera;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _dead;
        private bool _wasDead;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Allow left/right mouse clicks to cycle through living teammates while dead.");
            _autoAdvance = Config.Bind("General", "AutoAdvanceWhenTargetDies", true,
                "Automatically select another living teammate if the current spectator target dies or leaves.");
            _distance = Config.Bind("Camera", "Distance", 3.5f,
                new ConfigDescription("Third-person camera distance behind the spectated dwarf.",
                    new AcceptableValueRange<float>(1.5f, 8f)));
            _height = Config.Bind("Camera", "Height", 1.45f,
                new ConfigDescription("Camera height above the spectated dwarf.",
                    new AcceptableValueRange<float>(0.5f, 4f)));

            Logger.LogInfo("Spectator Switch v" + PluginVersion + " initialized (client-side). LMB: next, RMB: previous while dead.");
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

            // Only take mouse clicks when the vanilla death input state is active.
            // This avoids interfering with pause, inventory, class, or other modal menus.
            if (InputManager.Instance == null ||
                InputManager.Instance.CurrentInputState != InputManager.InputState.Dead ||
                Mouse.current == null)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                Cycle(1);
            else if (Mouse.current.rightButton.wasPressedThisFrame)
                Cycle(-1);
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
            Vector3 focus = t.position + Vector3.up * 1.15f;
            Vector3 backwards = -(t.forward.sqrMagnitude > 0.001f ? t.forward.normalized : Vector3.forward);
            Vector3 desired = focus + backwards * _distance.Value + Vector3.up * _height.Value;

            // Pull camera forward when terrain blocks the view, but ignore the observed dwarf's collider.
            RaycastHit hit;
            Vector3 rayOrigin = focus + Vector3.up * 0.45f;
            if (Physics.Linecast(rayOrigin, desired, out hit, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider != null && !hit.collider.transform.IsChildOf(t))
            {
                Vector3 fromWall = (rayOrigin - desired).normalized;
                desired = hit.point + fromWall * 0.2f;
            }

            _originalPosition = camera.transform.position;
            _originalRotation = camera.transform.rotation;
            _overriddenCamera = camera;
            camera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(focus - desired, Vector3.up));
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
            camera.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
            _overriddenCamera = null;
        }

        private void ResetSpectator()
        {
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

        private void OnDestroy() => ResetSpectator();
    }
}
