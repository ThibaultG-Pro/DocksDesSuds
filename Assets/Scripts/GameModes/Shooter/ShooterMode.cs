using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Wave shooter: drones fly at the player in increasingly hard waves.
    /// Game over when HP reaches 0; best score is saved in PlayerPrefs.
    /// </summary>
    public class ShooterMode : GameModeBase
    {
        [Header("Prefabs")]
        [SerializeField] ShooterGun gunPrefab;
        [SerializeField] ShooterEnemy dronePrefab;
        [SerializeField] ShooterEnemy heavyPrefab;

        [Tooltip("TMP overlay shader so the HUD draws on top of walls (referenced here so it's included in builds).")]
        [SerializeField] Shader hudOverlayShader;

        [Header("Gun placement on the right controller")]
        [SerializeField] Vector3 gunLocalPosition = new Vector3(0f, -0.02f, 0.02f);
        [SerializeField] Vector3 gunLocalEuler = new Vector3(0f, 0f, 0f);

        [Header("Player")]
        [SerializeField] int maxHp = 5;

        [Header("Spawning")]
        [Tooltip("World-space box enemies are allowed to spawn in (the building).")]
        [SerializeField] Bounds spawnArea = new Bounds(new Vector3(-0.8f, 3.5f, -1.2f), new Vector3(120f, 7f, 50f));
        [SerializeField] Vector2 spawnDistance = new Vector2(9f, 15f);
        [SerializeField] Vector2 spawnHeight = new Vector2(1.0f, 3.0f);

        [Header("Waves")]
        [SerializeField] int baseEnemiesPerWave = 4;
        [SerializeField] int extraEnemiesPerWave = 2;
        [SerializeField] float baseSpawnInterval = 1.8f;
        [SerializeField] float minSpawnInterval = 0.5f;
        [SerializeField] float speedIncreasePerWave = 0.08f;
        [SerializeField] int heavyFromWave = 3;
        [SerializeField] float pauseBetweenWaves = 3f;

        const string BestScoreKey = "Shooter.BestScore";

        readonly List<ShooterEnemy> _alive = new();
        ShooterGun _gun;
        ShooterHUD _hud;
        int _hp, _score, _wave, _shots, _hits;
        bool _gameOver;

        protected override void OnBegin()
        {
            _hp = maxHp;
            _score = _wave = _shots = _hits = 0;
            _gameOver = false;

            if (Manager.RightHand && gunPrefab)
            {
                Manager.SetRightHandDefaultsVisible(false);
                _gun = Instantiate(gunPrefab, Manager.RightHand);
                _gun.transform.localPosition = gunLocalPosition;
                _gun.transform.localRotation = Quaternion.Euler(gunLocalEuler);
                _gun.Fired += OnFired;
            }

            _hud = ShooterHUD.Create(Manager.Head, hudOverlayShader);
            RefreshHud();
            StartCoroutine(GameLoop());
        }

        protected override void OnEnd()
        {
            foreach (var e in _alive) if (e) e.Despawn();
            _alive.Clear();
            if (_gun) Destroy(_gun.gameObject);
            if (_hud) Destroy(_hud.gameObject);
            _gun = null;
            _hud = null;
        }

        IEnumerator GameLoop()
        {
            _hud.ShowMessage("MODE TIR", 1.5f, new Color(1f, 0.6f, 0.2f));
            yield return new WaitForSeconds(1.5f);
            for (int i = 3; i > 0; i--)
            {
                _hud.ShowMessage(i.ToString(), 0.8f);
                ShooterSfx.Play(ShooterSfx.Button, Manager.Head.position, 0.5f);
                yield return new WaitForSeconds(1f);
            }

            while (!_gameOver)
            {
                _wave++;
                RefreshHud();
                _hud.ShowMessage($"VAGUE {_wave}", 2f, new Color(0.4f, 0.9f, 1f));
                ShooterSfx.Play(ShooterSfx.Wave, Manager.Head.position, 0.6f);

                int count = baseEnemiesPerWave + extraEnemiesPerWave * (_wave - 1);
                float interval = Mathf.Max(minSpawnInterval, baseSpawnInterval - 0.15f * (_wave - 1));
                float speedMul = 1f + speedIncreasePerWave * (_wave - 1);
                float heavyChance = _wave >= heavyFromWave ? Mathf.Clamp((_wave - heavyFromWave + 1) * 0.12f, 0f, 0.4f) : 0f;

                for (int i = 0; i < count && !_gameOver; i++)
                {
                    var prefab = heavyPrefab && Random.value < heavyChance ? heavyPrefab : dronePrefab;
                    Spawn(prefab, speedMul);
                    yield return new WaitForSeconds(interval);
                }

                while (!_gameOver && _alive.Count > 0) yield return null;
                if (_gameOver) break;

                _hud.ShowMessage("VAGUE TERMINÉE", pauseBetweenWaves * 0.8f, new Color(0.4f, 1f, 0.5f));
                yield return new WaitForSeconds(pauseBetweenWaves);
            }
        }

        void Spawn(ShooterEnemy prefab, float speedMul)
        {
            if (!prefab) return;
            Vector3 head = Manager.Head.position;
            Vector3 pos = head;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(spawnDistance.x, spawnDistance.y);
                pos = new Vector3(head.x + c.x, Random.Range(spawnHeight.x, spawnHeight.y), head.z + c.y);
                if (spawnArea.Contains(pos)) break;
                pos = spawnArea.ClosestPoint(pos);
            }

            var enemy = Instantiate(prefab, pos, Quaternion.LookRotation(head - pos));
            enemy.Init(this, Manager.Head, speedMul);
            _alive.Add(enemy);
        }

        void OnFired(bool hit)
        {
            _shots++;
            if (hit) _hits++;
        }

        public void OnEnemyKilled(ShooterEnemy enemy)
        {
            _alive.Remove(enemy);
            _score += enemy.ScoreValue;
            RefreshHud();
        }

        public void OnEnemyReachedPlayer(ShooterEnemy enemy)
        {
            _alive.Remove(enemy);
            if (_gameOver) return;

            _hp = Mathf.Max(0, _hp - enemy.ContactDamage);
            RefreshHud();
            ShooterSfx.Play(ShooterSfx.Hurt, Manager.Head.position, 0.8f);
            Buzz(Manager.LeftHand, 0.8f, 0.25f);
            Buzz(Manager.RightHand, 0.8f, 0.25f);

            if (_hp <= 0) StartCoroutine(GameOverRoutine());
            else _hud.ShowMessage("TOUCHÉ !", 0.7f, new Color(1f, 0.25f, 0.25f));
        }

        IEnumerator GameOverRoutine()
        {
            _gameOver = true;
            foreach (var e in _alive) if (e) e.Despawn();
            _alive.Clear();

            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool record = _score > best;
            if (record) { PlayerPrefs.SetInt(BestScoreKey, _score); PlayerPrefs.Save(); }
            int accuracy = _shots > 0 ? Mathf.RoundToInt(100f * _hits / _shots) : 0;

            ShooterSfx.Play(ShooterSfx.GameOver, Manager.Head.position, 0.8f);
            _hud.ShowMessage(
                $"GAME OVER\n<size=60%>Score {_score}  •  Vague {_wave}  •  Précision {accuracy}%\n" +
                (record ? "<color=#ffd040>NOUVEAU RECORD !</color>" : $"Record {best}") + "</size>",
                6f, new Color(1f, 0.35f, 0.3f));

            yield return new WaitForSeconds(6f);
            End();
        }

        void RefreshHud()
        {
            if (_hud) _hud.SetStatus(_hp, maxHp, _score, _wave);
        }

        static void Buzz(Transform hand, float amplitude, float duration)
        {
            if (!hand) return;
            var h = hand.GetComponentInParent<HapticImpulsePlayer>();
            if (h) h.SendHapticImpulse(amplitude, duration);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
            Gizmos.DrawWireCube(spawnArea.center, spawnArea.size);
        }
    }
}
