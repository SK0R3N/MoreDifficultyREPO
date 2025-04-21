using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DifficultyFeature
{
    public interface ISlotEvent
    {
        string EventName { get; }
        string IconName { get; }
        string Asset {  get; }
        void Execute(); 
    }

    public static class SlotEventManager
    {
        private static List<ISlotEvent> registeredEvents = new();
        private static Dictionary<string, Sprite> eventIcons = new();

        public static void RegisterEvent(ISlotEvent e)
        {
            registeredEvents.Add(e);
        }

        public static void LoadEventIcons()
        {
            foreach (var ev in registeredEvents)
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(ev.Asset);
                Sprite icon = bundle.LoadAsset<Sprite>(ev.IconName);
                if (icon != null)
                {
                    eventIcons[ev.EventName] = icon;
                    Debug.Log($"[SlotEventManager] Loaded icon for event: {ev.EventName}");
                }
                else
                {
                    Debug.LogWarning($"[SlotEventManager] Icon not found: {ev.IconName}");
                }
            }
        }

        public static ISlotEvent GetRandomEvent()
        {
            if (registeredEvents.Count == 0)
                return null;

            return registeredEvents[UnityEngine.Random.Range(0, registeredEvents.Count)];
        }

        public static Sprite GetIconForEvent(string eventName)
        {
            return eventIcons.TryGetValue(eventName, out var icon) ? icon : null;
        }
    }
}
