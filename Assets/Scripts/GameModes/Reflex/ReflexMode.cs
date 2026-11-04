using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Reflex Rush: pads arranged in an arc around the player light up.
    /// Slap green pads with either hand before they fade, avoid red ones.
    /// Chain hits for a combo multiplier. 60 seconds, best score saved.
    /// </summary>
    public class ReflexMode : GameModeBase
    {
        [Header("Visuals")]
        [SerializeField] Material padMaterial;
        [SerializeField] Shader hudOverlayShader;

        [Header("Layout (relative to the player's head when the game starts)")]
        [SerializeField] int padsPerRow = 5;
        [SerializeField] float arcDegrees = 150f;
        [SerializeField] float radius = 0.6f;
        [SerializeField] float upperRowOffset = -0.15f;
        [SerializeField] float lowerRowOffset = -0.55f;
        [SerializeField] float padDiameter = 0.17f;

        [Header("Rules")]
        [SerializeField] float duration = 60f;
        [SerializeField] float touchRadius = 0.14f;
        [SerializeField] Vector2 litTime = new Vector2(1.6f, 0.65f);      // start -> end of game
        [SerializeField] Vector2 spawnInterval = new Vector2(0.9f, 0.35f); // start -> end of game
        [SerializeField] float trapChance = 0.18f;
        [SerializeField] float trapsFromSecond = 12f;
        [SerializeField] int pointsPerHit = 10;
        [SerializeField] int trapPenalty = 20;
        [SerializeField] int maxCombo = 5;

        const string BestScoreKey = "Reflex.BestScore";

        readonly List<ReflexPad> _pads = new();
        Transform _padRoot;
        ShooterHUD _hud;
        int _score, _combo, _hits, _misses, _traps;
        float _timeLeft;
        bool _playing;

        protected override void OnBegin()
        {
            _score = _combo = _hits = _misses = _traps = 0;
            _playing = false;
            _hud = ShooterHUD.Create(Manager.Head, hudOverlayShader);
            _hud.SetStatusText("");
            StartCoroutine(GameLoop());
        }

        protected override void OnEnd()
        {
            _playing = false;
            if (_padRoot) Destroy(_padRoot.gameObject);
            _pads.Clear();
            if (_hud) Destroy(_hud.gameObject);
            _hud = null;
        }

        IEnumerator GameLoop()
        {
            _hud.ShowMessage("REFLEX RUSH\n<size=40%>Tape les pastilles VERTES avec les mains • évite les ROUGES</size>", 3f, new Color(0.3f, 1f, 0.5f));
            yield return new WaitForSeconds(1.2f);
            BuildPads();
            yield return new WaitForSeconds(1.8f);
            for (int i = 3; i > 0; i--)
            {
                _hud.ShowMessage(i.ToString(), 0.8f);
                ShooterSfx.Play(ShooterSfx.Button, Manager.Head.position, 0.5f);
                yield return new WaitForSeconds(1f);
            }
            _hud.ShowMessage("GO !", 0.8f, new Color(0.3f, 1f, 0.5f));
            ShooterSfx.Play(ShooterSfx.Wave, Manager.Head.position, 0.6f);

            _timeLeft = duration;
            _playing = true;
            float nextSpawn = 0f;
            while (_timeLeft > 0f)
            {
                float progress = 1f - _timeLeft / duration;
                nextSpawn -= Time.deltaTime;
                if (nextSpawn <= 0f)
                {
                    nextSpawn = Mathf.Lerp(spawnInterval.x, spawnInterval.y, progress);
                    LightRandomPad(progress);
                }
                _timeLeft -= Time.deltaTime;
                RefreshHud();
                yield return null;
            }
            _playing = false;
            foreach (var p in _pads) p.Clear(Color.black);

            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool record = _score > best;
            if (record) { PlayerPrefs.SetInt(BestScoreKey, _score); PlayerPrefs.Save(); }
            int total = _hits + _misses;
            int accuracy = total > 0 ? Mathf.RoundToInt(100f * _hits / total) : 0;
            ShooterSfx.Play(ShooterSfx.GameOver, Manager.Head.position, 0.8f);
            _hud.ShowMessage(
                $"TEMPS ÉCOULÉ !\n<size=60%>Score {_score}  •  {_hits} touchées  •  Réussite {accuracy}%\n" +
                (record ? "<color=#ffd040>NOUVEAU RECORD !</color>" : $"Record {best}") + "</size>",
                6f, new Color(0.4f, 0.9f, 1f));
            yield return new WaitForSeconds(6f);
            End();
        }

        void BuildPads()
        {
            _padRoot = new GameObject("Reflex Pads").transform;
            Transform head = Manager.Head;
            Vector3 center = head.position;
            Vector3 fwd = ArrivalPoint ? ArrivalPoint.forward : head.forward;
            fwd.y = 0f;
            fwd.Normalize();

            foreach (float rowOffset in new[] { upperRowOffset, lowerRowOffset })
            {
                for (int i = 0; i < padsPerRow; i++)
                {
                    float t = padsPerRow == 1 ? 0.5f : i / (float)(padsPerRow - 1);
                    // Stagger the lower row so pads don't line up vertically.
                    float angle = Mathf.Lerp(-arcDegrees, arcDegrees, t) * 0.5f + (rowOffset == lowerRowOffset ? arcDegrees / padsPerRow * 0.25f : 0f);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * fwd;
                    Vector3 pos = center + dir * radius + Vector3.up * rowOffset;
                    Vector3 facing = (new Vector3(center.x, pos.y, center.z) - pos).normalized;
                    // Pad's up axis (where the timer disc sits) must point toward the player.
                    _pads.Add(ReflexPad.Create(_padRoot, padMaterial, pos, facing, padDiameter));
                }
            }
            ShooterSfx.Play(ShooterSfx.Teleport, center, 0.4f);
        }

        void LightRandomPad(float progress)
        {
            var ready = _pads.FindAll(p => p.IsReady);
            if (ready.Count == 0) return;
            var pad = ready[Random.Range(0, ready.Count)];
            bool trap = duration - _timeLeft > trapsFromSecond && Random.value < trapChance;
            float lit = Mathf.Lerp(litTime.x, litTime.y, progress) * (trap ? 1.4f : 1f);
            pad.Activate(trap ? ReflexPad.State.Trap : ReflexPad.State.Target, lit);
        }

        void Update()
        {
            if (!IsRunning || _pads.Count == 0) return;
            float dt = Time.deltaTime;
            foreach (var pad in _pads)
            {
                if (pad.Tick(dt) && _playing)
                {
                    if (pad.Current == ReflexPad.State.Target)
                    {
                        _misses++;
                        _combo = 0;
                        pad.Clear(new Color(0.4f, 0f, 0f));
                    }
                    else pad.Clear(Color.black); // trap faded: well avoided
                }
            }
            if (!_playing) return;

            CheckHand(Manager.LeftHand);
            CheckHand(Manager.RightHand);
        }

        void CheckHand(Transform hand)
        {
            if (!hand || !hand.gameObject.activeInHierarchy) return;
            foreach (var pad in _pads)
            {
                if (pad.Current == ReflexPad.State.Idle) continue;
                if (Vector3.Distance(hand.position, pad.transform.position) > touchRadius) continue;

                if (pad.Current == ReflexPad.State.Target)
                {
                    _hits++;
                    _combo = Mathf.Min(_combo + 1, maxCombo * 3);
                    _score += pointsPerHit * Multiplier;
                    pad.Clear(new Color(0.6f, 1f, 0.7f));
                    // Pitch climbs with the multiplier: audible reward for keeping the combo.
                    ShooterSfx.Play(ShooterSfx.Pop, pad.transform.position, 0.7f, 0.03f, 1f + 0.12f * (Multiplier - 1));
                    if (_combo > 0 && _combo % 3 == 0 && _combo <= (maxCombo - 1) * 3) // multiplier just went up
                        ShooterSfx.Play(ShooterSfx.Wave, Manager.Head.position, 0.35f, 0f, 1f + 0.1f * Multiplier);
                    Buzz(hand, 0.5f, 0.06f);
                }
                else
                {
                    _traps++;
                    _combo = 0;
                    _score = Mathf.Max(0, _score - trapPenalty);
                    pad.Clear(new Color(1f, 0.2f, 0.1f));
                    ShooterSfx.Play(ShooterSfx.Bad, pad.transform.position, 0.8f);
                    Buzz(hand, 1f, 0.25f);
                    _hud.ShowMessage("AÏE !", 0.5f, new Color(1f, 0.3f, 0.3f));
                }
            }
        }

        int Multiplier => 1 + Mathf.Min(maxCombo - 1, _combo / 3);

        void RefreshHud()
        {
            if (!_hud) return;
            string combo = Multiplier > 1 ? $"<color=#ffd040>x{Multiplier}</color>" : "x1";
            _hud.SetStatusText($"TEMPS <b>{Mathf.CeilToInt(Mathf.Max(0f, _timeLeft))}</b>    SCORE <b>{_score}</b>    COMBO {combo}");
        }

        static void Buzz(Transform hand, float amplitude, float duration)
        {
            var h = hand ? hand.GetComponentInParent<HapticImpulsePlayer>() : null;
            if (h) h.SendHapticImpulse(amplitude, duration);
        }
    }
}
