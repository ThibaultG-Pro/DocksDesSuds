using System;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Base class for every mini-game. A mode is started / stopped by <see cref="GameModeManager"/>.
    /// If a zone + arrival point are set, the manager teleports the player there on start
    /// and back to the hub when the mode ends.
    /// Subclasses implement OnBegin / OnEnd and call End() themselves when the game is over.
    /// </summary>
    public abstract class GameModeBase : MonoBehaviour
    {
        [SerializeField] string displayName = "Mode";
        [Tooltip("Background music played by AudioDirector while this mode runs.")]
        [SerializeField] MusicTrack music = MusicTrack.None;

        [Header("Location")]
        [Tooltip("Zone enabled while the mode runs (teleport floor + invisible walls).")]
        [SerializeField] PlayZone zone;
        [Tooltip("Where the player is teleported when the mode starts (position on the floor, forward = facing).")]
        [SerializeField] Transform arrivalPoint;

        public string DisplayName => displayName;
        public MusicTrack Music => music;
        public PlayZone Zone => zone;
        public Transform ArrivalPoint => arrivalPoint;
        public bool IsRunning { get; private set; }
        protected GameModeManager Manager { get; private set; }

        public event Action<GameModeBase> Ended;

        public void Begin(GameModeManager manager)
        {
            if (IsRunning) return;
            Manager = manager;
            IsRunning = true;
            OnBegin();
        }

        public void End()
        {
            if (!IsRunning) return;
            IsRunning = false;
            StopAllCoroutines();
            OnEnd();
            Ended?.Invoke(this);
        }

        protected abstract void OnBegin();
        protected abstract void OnEnd();
    }
}
