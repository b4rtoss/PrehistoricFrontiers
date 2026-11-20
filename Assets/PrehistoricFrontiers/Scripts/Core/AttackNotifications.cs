using System.Collections.Generic;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class AttackNotifications : MonoBehaviour
    {
        public sealed class Alert
        {
            public int Hits;
            public Transform Target;
            public Vector3 LastPosition;
            public string Name;
            public bool IsBase, IsWorker;
            public float LastHitAt, LastSoundAt = -100;
            public float Height;
            public string Text => IsBase ? (Target && Target.gameObject.activeInHierarchy ? Name + " под атакой!" : Name + " уничтожена!")
                : Target && Target.gameObject.activeInHierarchy ? (IsWorker ? "Рабочий под атакой!" : Name + " под атакой!")
                : (IsWorker ? "Рабочий погиб" : "Юнит потерян: " + Name);
        }
        public RTSSelectionController controller;
        public const int MaxAlerts = 4;
        public const float Lifetime = 8, RepeatSoundDelay = 4;
        readonly List<Alert> alerts = new List<Alert>();
        readonly Dictionary<Transform, Alert> records = new Dictionary<Transform, Alert>();
        public IReadOnlyList<Alert> Alerts => alerts;
        public int SoundCount { get; private set; }
        public AudioClip WarningSound { get; private set; }
        AudioSource speaker;
        float lastSoundAt = -100;
        float Clock => controller ? controller.UnpausedTime : Time.unscaledTime;
        static AttackNotifications uiOwner;
        GUIStyle card, marker, heading, empty;
        Vector2 panelScroll;
        public static int VisibleCount => uiOwner ? uiOwner.alerts.Count : 0;
        public static Rect PanelRect => HudLayout.Current.Alerts;
        void Awake()
        {
            uiOwner = this;
            speaker = gameObject.AddComponent<AudioSource>(); speaker.playOnAwake = false; speaker.spatialBlend = 0; speaker.volume = .18f;
            int samples = 5292;
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(i * Mathf.PI * 2 * 880 / 44100f) * Mathf.Sin(i * Mathf.PI / samples) * .5f;
            WarningSound = AudioClip.Create("Under attack notification", samples, 1, 44100, false); WarningSound.SetData(data, 0);
        }
        public static bool IsOverPanel(Vector3 mouse)
        {
            return uiOwner && uiOwner.alerts.Count > 0 && PanelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }
        public void Notify(RTSUnit unit)
        {
            if (!unit || !unit.IsAlive || unit.team != UnitTeam.Player) return;
            Add(unit.transform, HudTheme.UnitName(unit.stats.displayName), false, unit.stats.isWorker, unit.Agent ? unit.Agent.height + .8f : 2.5f);
        }
        public void Notify(BuildingHealth building)
        {
            if (!building || !building.IsAlive || building.team != UnitTeam.Player) return;
            var site = building.GetComponent<ConstructionSite>();
            Add(building.transform, site ? ConstructionSystem.Name(site.kind) : "База", true, false, 4);
        }
        void Add(Transform target, string name, bool isBase, bool isWorker, float height)
        {
            if (!controller || controller.MatchOver) return;
            PruneExpired();
            if (!records.TryGetValue(target, out var entry))
            {
                entry = new Alert { Target = target, Name = name, IsBase = isBase, IsWorker = isWorker, Height = height };
                records.Add(target, entry);
            }
            entry.LastPosition = target.position; entry.LastHitAt = Clock; entry.Hits++;
            RebuildVisible();
            if (Clock - entry.LastSoundAt >= RepeatSoundDelay && Clock - lastSoundAt >= 1)
            {
                entry.LastSoundAt = lastSoundAt = Clock;
                speaker.pitch = isBase ? .8f : 1; speaker.PlayOneShot(WarningSound); SoundCount++;
            }
        }
        void RebuildVisible()
        {
            alerts.Clear(); alerts.AddRange(records.Values);
            alerts.Sort((a, b) => a.IsBase != b.IsBase ? (a.IsBase ? -1 : 1) : b.LastHitAt.CompareTo(a.LastHitAt));
            if (alerts.Count > MaxAlerts) alerts.RemoveRange(MaxAlerts, alerts.Count - MaxAlerts);
        }
        void PruneExpired()
        {
            var expired = new List<Transform>();
            foreach (var entry in records.Values) if (Clock - entry.LastHitAt > Lifetime) expired.Add(entry.Target);
            if (expired.Count == 0) return;
            foreach (var target in expired) records.Remove(target);
            RebuildVisible();
        }
        void Update() { PruneExpired(); }
        public bool Focus(Alert entry)
        {
            if (MatchResultsScreen.IsVisible || entry == null || !controller || !controller.sceneCamera) return false;
            var cameraController = controller.sceneCamera.GetComponent<RTSCameraController>();
            if (!cameraController) return false;
            cameraController.FocusOn(entry.Target && entry.Target.gameObject.activeInHierarchy ? entry.Target.position : entry.LastPosition);
            return true;
        }
        public void DrawPanel()
        {
            if (card == null)
            {
                card = HudTheme.AlertButton(GUI.skin.button);
                card.wordWrap = true;
                card.alignment = TextAnchor.MiddleLeft;
                heading = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                empty = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                marker = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                marker.normal.textColor = new Color(1, .15f, .1f);
            }
            var layout = HudLayout.Current;
            card.fontSize = layout.Font(13); heading.fontSize = layout.Font(15); empty.fontSize = layout.Font(13);
            card.padding = new RectOffset(Mathf.RoundToInt(8 * layout.Scale), Mathf.RoundToInt(8 * layout.Scale), Mathf.RoundToInt(3 * layout.Scale), Mathf.RoundToInt(3 * layout.Scale));
            var oldBackground = GUI.backgroundColor; var oldContent = GUI.contentColor;
            HudLayout.DrawPanel(layout.Alerts);
            GUI.Label(layout.AlertTitle, "УВЕДОМЛЕНИЯ" + (alerts.Count > 0 ? " / " + alerts.Count : ""), heading);
            if (alerts.Count == 0)
                GUI.Label(layout.AlertViewport, "Новых угроз нет", empty);
            else
            {
                GUI.backgroundColor = Color.white; GUI.contentColor = Color.white;
                float width = layout.AlertViewport.width - 22;
                panelScroll = GUI.BeginScrollView(layout.AlertViewport, panelScroll, new Rect(0, 0, width, alerts.Count * 56 * layout.Scale));
                for (int i = 0; i < alerts.Count; i++)
                {
                    var entry = alerts[i];
                    if (GUI.Button(new Rect(0, i * 56 * layout.Scale, width, 50 * layout.Scale), entry.Text + "\nПерейти к цели • ударов: " + entry.Hits, card)) Focus(entry);
                }
                GUI.EndScrollView();
            }
            GUI.backgroundColor = oldBackground; GUI.contentColor = oldContent;
        }
        public void DrawMarkers()
        {
            if (marker == null) return;
            var layout = HudLayout.Current; marker.fontSize = layout.Font(30);
            foreach (var entry in records.Values)
            {
                if (!entry.Target || !entry.Target.gameObject.activeInHierarchy || Clock - entry.LastHitAt > 4) continue;
                var p = controller.sceneCamera.WorldToScreenPoint(entry.Target.position + Vector3.up * entry.Height);
                if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height || BattleHUD.IsOverPanel(p)) continue;
                var bounds = new Rect(p.x - 16 * layout.Scale, Screen.height - p.y - 20 * layout.Scale, 32 * layout.Scale, 40 * layout.Scale);
                if (!layout.OverlapsPanel(bounds)) GUI.Label(bounds, "!", marker);
            }
        }
        void OnDestroy()
        {
            if (uiOwner == this) uiOwner = null;
            if (WarningSound) Destroy(WarningSound);
        }
    }
}
