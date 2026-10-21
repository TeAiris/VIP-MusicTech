using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>
    /// Listens to area trigger zones and shows the matching popup when the player enters.
    /// Last entered zone wins if volumes overlap.
    /// </summary>
    public sealed class AreaPopupDirector : MonoBehaviour
    {
        [SerializeField] private List<AreaTriggerZone> zones = new List<AreaTriggerZone>();
        [SerializeField] private List<AreaPopupPanel> panels = new List<AreaPopupPanel>();
        [SerializeField] private bool hideWhenLeavingAllAreas = true;

        public UnityEvent<StudioAreaId> AreaActivated = new UnityEvent<StudioAreaId>();
        public UnityEvent AreaCleared = new UnityEvent();

        private readonly Stack<AreaTriggerZone> enterStack = new Stack<AreaTriggerZone>();
        private AreaTriggerZone activeZone;

        public AreaTriggerZone ActiveZone => activeZone;

        public void SetZones(List<AreaTriggerZone> triggerZones) => zones = triggerZones ?? new List<AreaTriggerZone>();
        public void SetPanels(List<AreaPopupPanel> popupPanels) => panels = popupPanels ?? new List<AreaPopupPanel>();

        private void OnEnable()
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                zones[i].PlayerEntered.AddListener(OnZoneEntered);
                zones[i].PlayerExited.AddListener(OnZoneExited);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                zones[i].PlayerEntered.RemoveListener(OnZoneEntered);
                zones[i].PlayerExited.RemoveListener(OnZoneExited);
            }
        }

        private void OnZoneEntered(AreaTriggerZone zone)
        {
            if (zone == null) return;
            PushZone(zone);
            ActivateZone(zone);
        }

        private void OnZoneExited(AreaTriggerZone zone)
        {
            if (zone == null) return;
            RemoveZone(zone);

            if (enterStack.Count > 0)
            {
                ActivateZone(enterStack.Peek());
                return;
            }

            activeZone = null;
            HideAllPanels();
            if (hideWhenLeavingAllAreas) AreaCleared.Invoke();
        }

        private void ActivateZone(AreaTriggerZone zone)
        {
            activeZone = zone;
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel == null) continue;
                if (panel.AreaId == zone.AreaId)
                {
                    panel.ApplyContent(zone.AreaTitle, zone.AreaHint);
                    panel.Show();
                }
                else
                {
                    panel.Hide();
                }
            }

            AreaActivated.Invoke(zone.AreaId);
        }

        private void HideAllPanels()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null) panels[i].Hide();
            }
        }

        private void PushZone(AreaTriggerZone zone)
        {
            // Avoid duplicate stack entries for the same zone.
            if (enterStack.Count > 0 && enterStack.Peek() == zone) return;
            var buffer = enterStack.ToArray();
            enterStack.Clear();
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i] != zone) enterStack.Push(buffer[i]);
            }
            enterStack.Push(zone);
        }

        private void RemoveZone(AreaTriggerZone zone)
        {
            if (enterStack.Count == 0) return;
            var buffer = enterStack.ToArray();
            enterStack.Clear();
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i] != zone) enterStack.Push(buffer[i]);
            }
        }
    }
}
