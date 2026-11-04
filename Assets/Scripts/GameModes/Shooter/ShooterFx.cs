using System.Collections.Generic;
using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>Cheap explosion: a handful of small emissive cubes flying out and shrinking.</summary>
    public static class ShooterFx
    {
        static readonly Dictionary<Color, Material> Materials = new();

        public static void Debris(Vector3 position, Color color, int count, float scale, int layer)
        {
            var mat = GetMaterial(color);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Debris";
                go.layer = layer;
                go.transform.position = position + Random.insideUnitSphere * 0.15f * scale;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * Random.Range(0.04f, 0.1f) * scale;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var rb = go.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.onUnitSphere * Random.Range(2f, 5f) * scale + Vector3.up * 1.5f;
                rb.angularVelocity = Random.insideUnitSphere * 15f;
                go.AddComponent<ShrinkAndDie>().life = Random.Range(0.6f, 1.1f);
            }
        }

        static Material GetMaterial(Color color)
        {
            if (Materials.TryGetValue(color, out var m) && m) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            m = new Material(shader) { name = "Debris" };
            m.SetColor("_BaseColor", color);
            Materials[color] = m;
            return m;
        }
    }

    public class ShrinkAndDie : MonoBehaviour
    {
        public float life = 1f;
        float _t;
        Vector3 _start;

        void Start() => _start = transform.localScale;

        void Update()
        {
            _t += Time.deltaTime;
            transform.localScale = _start * Mathf.Clamp01(1f - _t / life);
            if (_t >= life) Destroy(gameObject);
        }
    }
}
