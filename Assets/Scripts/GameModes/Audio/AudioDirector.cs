using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Background audio: hall ambience everywhere, plus a music loop per activity that fades in
    /// when the mode starts (ambience ducks) and fades out when it ends.
    /// Loops are synthesized on a worker thread at startup, so there is no hitch and no audio asset.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [SerializeField] float ambienceVolume = 0.3f;
        [SerializeField] float ambienceDuckedVolume = 0.1f;
        [SerializeField] float musicVolume = 0.35f;
        [SerializeField] float fadeSeconds = 1.5f;

        AudioSource _ambience, _music;
        AudioClip _actionClip, _upbeatClip;
        MusicTrack _wanted = MusicTrack.None;
        float _musicTarget;

        void Awake()
        {
            _ambience = MakeSource("Ambience");
            _music = MakeSource("Music");
        }

        AudioSource MakeSource(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 64;
            return s;
        }

        void OnEnable()
        {
            StartCoroutine(Hook());
            StartCoroutine(Synthesize());
        }

        IEnumerator Hook()
        {
            while (GameModeManager.Instance == null) yield return null;
            GameModeManager.Instance.ModeStarted += OnModeStarted;
            GameModeManager.Instance.ModeEnded += OnModeEnded;
        }

        void OnDisable()
        {
            if (GameModeManager.Instance == null) return;
            GameModeManager.Instance.ModeStarted -= OnModeStarted;
            GameModeManager.Instance.ModeEnded -= OnModeEnded;
        }

        IEnumerator Synthesize()
        {
            var amb = Task.Run(ProceduralMusic.Ambience);
            var action = Task.Run(ProceduralMusic.Action);
            var upbeat = Task.Run(ProceduralMusic.Upbeat);

            while (!amb.IsCompleted) yield return null;
            if (amb.Status == TaskStatus.RanToCompletion)
            {
                _ambience.clip = ToClip("Hall Ambience", amb.Result);
                _ambience.Play();
            }
            while (!action.IsCompleted || !upbeat.IsCompleted) yield return null;
            if (action.Status == TaskStatus.RanToCompletion) _actionClip = ToClip("Music Action", action.Result);
            if (upbeat.Status == TaskStatus.RanToCompletion) _upbeatClip = ToClip("Music Upbeat", upbeat.Result);
            ApplyTrack(); // a mode may already be running
        }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, ProceduralMusic.SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnModeStarted(GameModeBase mode)
        {
            _wanted = mode.Music;
            ApplyTrack();
        }

        void OnModeEnded(GameModeBase mode)
        {
            _wanted = MusicTrack.None;
            _musicTarget = 0f;
        }

        void ApplyTrack()
        {
            var clip = _wanted switch
            {
                MusicTrack.Action => _actionClip,
                MusicTrack.Upbeat => _upbeatClip,
                _ => null,
            };
            if (clip == null) { _musicTarget = 0f; return; }
            if (_music.clip != clip || !_music.isPlaying)
            {
                _music.clip = clip;
                _music.volume = 0f;
                _music.Play();
            }
            _musicTarget = musicVolume;
        }

        void Update()
        {
            float speed = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            float ambTarget = _musicTarget > 0f ? ambienceDuckedVolume : ambienceVolume;
            _ambience.volume = Mathf.MoveTowards(_ambience.volume, ambTarget, speed);
            _music.volume = Mathf.MoveTowards(_music.volume, _musicTarget, speed);
            if (_musicTarget <= 0f && _music.isPlaying && _music.volume <= 0.001f) _music.Stop();
        }
    }
}
