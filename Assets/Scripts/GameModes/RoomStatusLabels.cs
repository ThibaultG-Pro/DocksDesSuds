using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// VR-friendly room information for every room inside <see cref="area"/> (the hall):
    ///  - a floating label above the room (name + LIBRE / OCCUPÉE), readable from far away;
    ///  - an info card at head height next to the door (status, current / next booking, capacity).
    /// Reads the same RoomDataSO the room tint uses.
    /// </summary>
    public class RoomStatusLabels : MonoBehaviour
    {
        [Tooltip("Only rooms whose center is inside this world box get a label (the hall).")]
        [SerializeField] Bounds area = new Bounds(new Vector3(-41.5f, 2f, -1.2f), new Vector3(41f, 10f, 53f));

        [Header("Floating label")]
        [SerializeField] float heightAboveRoom = 0.5f;
        [SerializeField] float fontSize = 3.5f;

        [Header("Info card")]
        [SerializeField] bool showCards = true;
        [SerializeField] Material cardMaterial;
        [SerializeField] float cardHeight = 1.5f;
        [SerializeField] Vector2 cardSize = new Vector2(0.75f, 0.55f);
        [Tooltip("Sideways offset from the door sign so the card doesn't cover the door.")]
        [SerializeField] float cardSideOffset = 0.75f;
        [Tooltip("Distance from the door sign toward the corridor (container walls stick out past the sign).")]
        [SerializeField] float cardWallOffset = 0.3f;

        [Header("Data")]
        [Tooltip("Load Resources/MockData/<roomCode>.json at start and stop BuildingManager's API refresh. " +
                 "Needed on the headset: the configured API (localhost) is unreachable there and every refresh " +
                 "would mark all rooms as unknown.")]
        [SerializeField] bool useMockData = true;

        class Label
        {
            public RoomController room;
            public TextMeshPro floating;
            public TextMeshPro card;
            public Renderer statusBar;
            public bool billboardCard;
        }

        readonly List<Label> _labels = new();
        MaterialPropertyBlock _mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            // All Awakes run before any Start: disabling here prevents BuildingManager.Start
            // from launching its auto-refresh against the unreachable API.
            if (useMockData)
            {
                var bm = FindAnyObjectByType<BuildingManager>();
                if (bm) bm.enabled = false;
            }
        }

        void Start()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (var room in FindObjectsByType<RoomController>(FindObjectsInactive.Exclude))
            {
                if (room.roomData == null) continue;
                var b = RendererBounds(room.transform);
                if (!area.Contains(b.center)) continue;

                var label = new Label { room = room };
                label.floating = MakeText("Label " + room.name, transform,
                    new Vector3(b.center.x, b.max.y + heightAboveRoom, b.center.z), fontSize, new Vector2(6f, 1.5f));
                label.floating.alignment = TextAlignmentOptions.Center;
                if (showCards) BuildCard(label, b);

                _labels.Add(label);
                room.roomData.OnDataUpdated += () => Refresh(label);
                if (useMockData) LoadMock(room.roomData);
                Refresh(label);
            }
        }

        void BuildCard(Label label, Bounds roomBounds)
        {
            var sign = label.room.GetComponentInChildren<RoomSign>(true);
            Vector3 pos;
            Quaternion rot;
            if (sign && Mathf.Abs(sign.transform.forward.y) < 0.5f)
            {
                // Door sign on a container wall: outward normal is -forward. Put the card beside the door.
                Vector3 outward = -sign.transform.forward;
                outward.y = 0f;
                outward.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                Vector3 towardCenter = roomBounds.center - sign.transform.position;
                if (Vector3.Dot(side, towardCenter) < 0f) side = -side; // move toward the middle of the wall
                pos = sign.transform.position + outward * cardWallOffset + side * cardSideOffset;
                pos.y = cardHeight;
                rot = Quaternion.LookRotation(-outward); // text readable from outside
            }
            else
            {
                // Open space (no wall): standing card, turned toward the player every frame.
                pos = sign ? sign.transform.position : roomBounds.center;
                pos.y = cardHeight;
                rot = Quaternion.identity;
                label.billboardCard = true;
            }

            var root = new GameObject("Card " + label.room.name).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(pos, rot);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(root, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            bg.transform.localScale = new Vector3(cardSize.x, cardSize.y, 1f);
            bg.transform.localRotation = Quaternion.identity;
            var bgR = bg.GetComponent<Renderer>();
            bgR.sharedMaterial = cardMaterial;
            bgR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetColor(bgR, new Color(0.05f, 0.07f, 0.12f));

            var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = "StatusBar";
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(root, false);
            bar.transform.localPosition = new Vector3(0f, cardSize.y * 0.5f - 0.02f, 0.003f);
            bar.transform.localScale = new Vector3(cardSize.x, 0.04f, 1f);
            label.statusBar = bar.GetComponent<Renderer>();
            label.statusBar.sharedMaterial = cardMaterial;
            label.statusBar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            label.card = MakeText("Text", root, Vector3.zero, 0.55f, cardSize - new Vector2(0.08f, 0.1f));
            label.card.transform.localPosition = new Vector3(0f, -0.015f, 0f);
            label.card.transform.localRotation = Quaternion.identity;
            label.card.alignment = TextAlignmentOptions.TopLeft;
            label.card.textWrappingMode = TextWrappingModes.Normal;
            label.card.enableAutoSizing = true;
            label.card.fontSizeMin = 0.25f;
            label.card.fontSizeMax = 0.55f;
            label.card.outlineWidth = 0f;
        }

        static TextMeshPro MakeText(string name, Transform parent, Vector3 worldPos, float size, Vector2 rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = rect;
            t.outlineWidth = 0.15f;
            t.outlineColor = new Color32(0, 0, 0, 220);
            return t;
        }

        static void LoadMock(RoomDataSO data)
        {
            var asset = Resources.Load<TextAsset>($"MockData/{data.roomCode}");
            if (asset == null) return;
            try { data.UpdateFromResponse(JsonUtility.FromJson<RoomApiResponse>(asset.text)); }
            catch (Exception e) { Debug.LogWarning($"[RoomStatusLabels] Mock parse error for {data.roomCode}: {e.Message}"); }
        }

        void Refresh(Label l)
        {
            var d = l.room.roomData;
            string name = string.IsNullOrEmpty(d.roomName) ? d.roomCode : d.roomName;
            (string txt, string hex, Color col) = d.status switch
            {
                "free" => ("LIBRE", "#2ECC71", new Color(0.18f, 0.8f, 0.44f)),
                "occupied" => ("OCCUPÉE", "#E74C3C", new Color(0.91f, 0.3f, 0.24f)),
                "upcoming" => ("BIENTÔT OCCUPÉE", "#F39C12", new Color(0.95f, 0.61f, 0.07f)),
                _ => ("INCONNU", "#95A5A6", new Color(0.5f, 0.55f, 0.55f)),
            };
            l.floating.text = $"<size=70%>{name}</size>\n<color={hex}><b>{txt}</b></color>";

            if (l.card == null) return;
            SetColor(l.statusBar, col);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=130%><b>{name}</b></size>\n");
            sb.Append($"<color={hex}><b>{txt}</b></color>\n");
            sb.Append("<line-height=40%>\n</line-height>");
            if (d.currentEvent != null && d.currentEvent.IsValid())
                sb.Append($"<color=#9fb3c8>En cours</color>  {EventLine(d.currentEvent)}\n");
            if (d.nextEvent != null && d.nextEvent.IsValid())
                sb.Append($"<color=#9fb3c8>Ensuite</color>  {EventLine(d.nextEvent)}\n");
            if ((d.currentEvent == null || !d.currentEvent.IsValid()) && (d.nextEvent == null || !d.nextEvent.IsValid()))
                sb.Append("<color=#9fb3c8>Aucune réservation à venir</color>\n");
            var info = new List<string>();
            if (d.capacity > 0) info.Add($"{d.capacity} pers.");
            if (!string.IsNullOrEmpty(d.roomType)) info.Add(d.roomType);
            if (info.Count > 0) sb.Append($"<size=85%><color=#9fb3c8>{string.Join("  •  ", info)}</color></size>");
            l.card.text = sb.ToString();
        }

        static string EventLine(EventData e)
        {
            string title = string.IsNullOrEmpty(e.title) ? "Réservé" : e.title;
            string time = $"{Hour(e.start)}–{Hour(e.end)}";
            string who = string.IsNullOrEmpty(e.organizer) ? "" : $"\n<size=85%><color=#9fb3c8>      par {e.organizer}</color></size>";
            return $"<b>{title}</b>  <size=85%>{time}</size>{who}";
        }

        static string Hour(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "?";
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
                ? dt.ToLocalTime().ToString("HH:mm")
                : "?";
        }

        void SetColor(Renderer r, Color c)
        {
            if (!r) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            Vector3 head = cam.transform.position;
            foreach (var l in _labels)
            {
                Billboard(l.floating.transform, head);
                if (l.billboardCard && l.card) Billboard(l.card.transform.parent, head);
            }
        }

        static void Billboard(Transform t, Vector3 head)
        {
            Vector3 dir = t.position - head;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(dir);
        }

        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
