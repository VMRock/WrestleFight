using System.Collections.Generic;
using UnityEngine;

namespace WrestleGame
{
    public class WrestlingVFX : MonoBehaviour
    {
        public static WrestlingVFX Instance { get; private set; }

        private class CombatText
        {
            public string id;
            public string text;
            public Vector3 worldPos;
            public Color color;
            public float life;
            public float maxLife;
            public float scale;
            public Vector3 velocity;
        }

        private class ImpactSpark
        {
            public Vector3 pos;
            public Vector3 vel;
            public Color color;
            public float life;
            public float maxLife;
            public float size;
        }

        private class ShockwaveRing
        {
            public Vector3 pos;
            public float radius;
            public float maxRadius;
            public float life;
            public float maxLife;
            public Color color;
        }

        private List<CombatText> combatTexts = new List<CombatText>();
        private List<ImpactSpark> sparks = new List<ImpactSpark>();
        private List<ShockwaveRing> shockwaves = new List<ShockwaveRing>();

        private float screenFlashAlpha = 0f;
        private Color screenFlashColor = Color.white;

        private Material lineMat;
        private GUIStyle combatTextStyle;
        private GUIStyle criticalTextStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateLineMaterial();
            SetupStyles();
        }

        private void CreateLineMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMat.SetInt("_ZWrite", 0);
        }

        private void SetupStyles()
        {
            combatTextStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            criticalTextStyle = new GUIStyle
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.1f) }
            };
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Update floating texts
            for (int i = combatTexts.Count - 1; i >= 0; i--)
            {
                var ct = combatTexts[i];
                ct.life += dt;
                ct.worldPos += ct.velocity * dt;
                ct.velocity.y *= 0.92f;
                if (ct.life >= ct.maxLife)
                {
                    combatTexts.RemoveAt(i);
                }
            }

            // Update sparks
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.life += dt;
                s.pos += s.vel * dt;
                s.vel *= 0.92f;
                if (s.life >= s.maxLife)
                {
                    sparks.RemoveAt(i);
                }
            }

            // Update shockwaves
            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                var sw = shockwaves[i];
                sw.life += dt;
                float progress = sw.life / sw.maxLife;
                sw.radius = Mathf.Lerp(0.2f, sw.maxRadius, progress);
                if (sw.life >= sw.maxLife)
                {
                    shockwaves.RemoveAt(i);
                }
            }

            // Screen flash fade
            if (screenFlashAlpha > 0f)
            {
                screenFlashAlpha = Mathf.Max(0f, screenFlashAlpha - dt * 2.5f);
            }
        }

        public void SpawnCombatText(string text, Vector3 worldPos, Color color, bool isCritical = false, string categoryId = "")
        {
            string id = string.IsNullOrEmpty(categoryId) ? text : categoryId;

            // If a combat text for this category/fighter already exists, replace it immediately so they never overlap!
            for (int i = combatTexts.Count - 1; i >= 0; i--)
            {
                if (combatTexts[i].id == id || Vector3.Distance(combatTexts[i].worldPos, worldPos) < 0.6f)
                {
                    combatTexts.RemoveAt(i);
                }
            }

            combatTexts.Add(new CombatText
            {
                id = id,
                text = text,
                worldPos = worldPos + Vector3.up * 0.45f,
                color = color,
                life = 0f,
                maxLife = isCritical ? 0.9f : 0.65f,
                scale = isCritical ? 1.3f : 1.0f,
                velocity = new Vector3(0, isCritical ? 1.4f : 1.1f, 0)
            });
        }

        public void ClearCombatTexts()
        {
            combatTexts.Clear();
        }

        public void SpawnHitSparks(Vector3 pos, Color sparkColor, int count = 12)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.3f;
                sparks.Add(new ImpactSpark
                {
                    pos = pos + Random.insideUnitSphere * 0.1f,
                    vel = dir.normalized * Random.Range(2.5f, 5.5f),
                    color = sparkColor,
                    life = 0f,
                    maxLife = Random.Range(0.25f, 0.45f),
                    size = Random.Range(0.04f, 0.09f)
                });
            }
        }

        public void SpawnSlamDust(Vector3 pos, float radius = 2.0f)
        {
            shockwaves.Add(new ShockwaveRing
            {
                pos = pos,
                radius = 0.2f,
                maxRadius = radius,
                life = 0f,
                maxLife = 0.6f,
                color = new Color(1f, 0.9f, 0.7f, 0.8f)
            });

            for (int i = 0; i < 20; i++)
            {
                float angle = i * (Mathf.PI * 2f / 20);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0.15f, Mathf.Sin(angle));
                sparks.Add(new ImpactSpark
                {
                    pos = pos,
                    vel = dir.normalized * Random.Range(3f, 6.5f),
                    color = new Color(0.95f, 0.85f, 0.65f, 0.9f),
                    life = 0f,
                    maxLife = Random.Range(0.35f, 0.6f),
                    size = Random.Range(0.06f, 0.12f)
                });
            }
        }

        public void TriggerScreenFlash(Color color, float alpha = 0.6f)
        {
            screenFlashColor = color;
            screenFlashAlpha = alpha;
        }

        private void OnRenderObject()
        {
            if (lineMat == null) return;
            lineMat.SetPass(0);

            // Draw Sparks
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                float alpha = 1f - (s.life / s.maxLife);
                Color c = new Color(s.color.r, s.color.g, s.color.b, alpha);
                GL.Color(c);

                Vector3 start = s.pos;
                Vector3 end = s.pos - s.vel.normalized * s.size;
                GL.Vertex(start);
                GL.Vertex(end);
            }

            // Draw Shockwaves
            for (int i = 0; i < shockwaves.Count; i++)
            {
                var sw = shockwaves[i];
                float alpha = 1f - (sw.life / sw.maxLife);
                Color c = new Color(sw.color.r, sw.color.g, sw.color.b, alpha * sw.color.a);
                GL.Color(c);

                int segments = 24;
                for (int j = 0; j < segments; j++)
                {
                    float a1 = j * (Mathf.PI * 2f / segments);
                    float a2 = (j + 1) * (Mathf.PI * 2f / segments);
                    Vector3 p1 = sw.pos + new Vector3(Mathf.Cos(a1) * sw.radius, 0.02f, Mathf.Sin(a1) * sw.radius);
                    Vector3 p2 = sw.pos + new Vector3(Mathf.Cos(a2) * sw.radius, 0.02f, Mathf.Sin(a2) * sw.radius);
                    GL.Vertex(p1);
                    GL.Vertex(p2);
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Screen flash
            if (screenFlashAlpha > 0.01f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(screenFlashColor.r, screenFlashColor.g, screenFlashColor.b, screenFlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Floating Combat Text
            for (int i = 0; i < combatTexts.Count; i++)
            {
                var ct = combatTexts[i];
                Vector3 screenPos = cam.WorldToScreenPoint(ct.worldPos);
                if (screenPos.z <= 0) continue;

                float alpha = 1f - Mathf.Pow(ct.life / ct.maxLife, 2f);
                Color c = ct.color;
                c.a = alpha;

                GUIStyle style = (ct.scale > 1.2f) ? criticalTextStyle : combatTextStyle;
                style.normal.textColor = c;

                float guiY = Screen.height - screenPos.y;
                float width = 240 * ct.scale;
                float height = 40 * ct.scale;
                Rect r = new Rect(screenPos.x - width * 0.5f, guiY - height * 0.5f, width, height);

                // Draw shadow for readability
                Color shadowCol = new Color(0, 0, 0, alpha * 0.9f);
                style.normal.textColor = shadowCol;
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), ct.text, style);

                // Draw main text
                style.normal.textColor = c;
                GUI.Label(r, ct.text, style);
            }
        }
    }
}

