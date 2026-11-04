using TMPro;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// World-space HUD that lazily follows the player's gaze (yaw only, smoothed) to stay comfortable in VR.
    /// Top line: HP / score / wave. Center: big transient messages.
    /// </summary>
    public class ShooterHUD : MonoBehaviour
    {
        [SerializeField] float distance = 2.4f;
        [SerializeField] float statusHeight = 0.75f;
        [SerializeField] float followSpeed = 3f;

        TextMeshPro _status;
        TextMeshPro _message;
        Transform _head;
        float _messageUntil;
        float _yaw;

        /// <param name="overlayShader">Optional TMP overlay shader so the HUD is never hidden by walls.</param>
        public static ShooterHUD Create(Transform head, Shader overlayShader = null)
        {
            var go = new GameObject("Shooter HUD");
            var hud = go.AddComponent<ShooterHUD>();
            hud._head = head;
            hud._status = MakeText(go.transform, "Status", 0.9f, new Vector3(0f, hud.statusHeight, 0f));
            hud._message = MakeText(go.transform, "Message", 2.2f, Vector3.zero);
            if (overlayShader)
            {
                hud._status.fontMaterial.shader = overlayShader;
                hud._message.fontMaterial.shader = overlayShader;
            }
            hud._yaw = head.eulerAngles.y;
            hud.SnapToHead();
            return hud;
        }

        static TextMeshPro MakeText(Transform parent, string name, float size, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = Color.white;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }

        public void SetStatus(int hp, int maxHp, int score, int wave)
        {
            string hearts = "<color=#ff4040>" + new string('|', Mathf.Max(0, hp)) + "</color><color=#555555>" + new string('|', Mathf.Max(0, maxHp - hp)) + "</color>";
            _status.text = $"PV {hearts}    SCORE <b>{score}</b>    VAGUE {wave}";
        }

        public void SetStatusText(string text) => _status.text = text;

        public void ShowMessage(string text, float seconds, Color? color = null)
        {
            _message.text = text;
            _message.color = color ?? Color.white;
            _messageUntil = Time.time + seconds;
        }

        void SnapToHead()
        {
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            transform.position = _head.position + fwd * distance;
            transform.rotation = Quaternion.LookRotation(fwd);
        }

        void LateUpdate()
        {
            if (_head == null) return;
            _yaw = Mathf.LerpAngle(_yaw, _head.eulerAngles.y, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            SnapToHead();

            float remaining = _messageUntil - Time.time;
            var c = _message.color;
            c.a = Mathf.Clamp01(remaining / 0.3f);
            _message.color = c;
        }
    }
}
