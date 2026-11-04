using System;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Base class for every mini-game. A mode is started / stopped by <see cref="GameModeManager"/>.
    /// Subclasses implement OnBegin / OnEnd and call End() themselves when the game is over.
    /// </summary>
    public abstract class GameModeBase : MonoBehaviour
    {
        [SerializeField] string displayName = "Mode";

        public string DisplayName => displayName;
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
