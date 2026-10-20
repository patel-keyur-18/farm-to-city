using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Game.Services
{
    /// <summary>One local notification to schedule: when (wall clock, UTC) and what it says.</summary>
    public readonly struct ScheduledNote
    {
        public long WallUtc { get; }
        public string Title { get; }
        public string Body { get; }

        /// <summary>Groups the notification in iOS's list (one thread per kind).</summary>
        public string Thread { get; }

        public ScheduledNote(long wallUtc, string title, string body, string thread)
        {
            WallUtc = wallUtc;
            Title = title;
            Body = body;
            Thread = thread;
        }
    }

    /// <summary>
    /// Local notifications (spec 007 §3.5, ADR 0041): a port with one implementation per platform. Only
    /// <see cref="IosNotificationService"/> touches Unity's Mobile Notifications package (2.5.0, pinned); if the package ever fails,
    /// a hand-written plugin replaces that one class. Notifications never change the town.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Asks the platform for permission (iOS shows its dialog only the first time); calls back with the answer.</summary>
        void RequestPermission(Action<bool> done);

        /// <summary>Schedules these notes, replacing any scheduled before.</summary>
        void Schedule(long nowWallUtc, IReadOnlyList<ScheduledNote> notes);

        /// <summary>Removes scheduled and delivered notifications (the player is back).</summary>
        void CancelAll();
    }

    public static class NotificationServices
    {
        public static INotificationService ForPlatform(MonoBehaviour host)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosNotificationService(host);
#else
            return new LoggingNotificationService();
#endif
        }
    }

    /// <summary>The Editor and untested platforms: logs what it would schedule (Android is untested, ADR 0026).</summary>
    public sealed class LoggingNotificationService : INotificationService
    {
        public List<ScheduledNote> Last { get; } = new List<ScheduledNote>();

        public void RequestPermission(Action<bool> done) { done(true); }

        public void Schedule(long nowWallUtc, IReadOnlyList<ScheduledNote> notes)
        {
            Last.Clear();
            Last.AddRange(notes);
            foreach (ScheduledNote n in notes) Debug.Log("Notification in " + (n.WallUtc - nowWallUtc) + " s: " + n.Title + " — " + n.Body);
        }

        public void CancelAll() { Last.Clear(); }
    }

#if UNITY_IOS
    public sealed class IosNotificationService : INotificationService
    {
        private readonly MonoBehaviour _host;

        public IosNotificationService(MonoBehaviour host) { _host = host; }

        public void RequestPermission(Action<bool> done) { _host.StartCoroutine(Request(done)); }

        private static IEnumerator Request(Action<bool> done)
        {
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false))
            {
                while (!req.IsFinished) yield return null;
                done(req.Granted);
            }
        }

        public void Schedule(long nowWallUtc, IReadOnlyList<ScheduledNote> notes)
        {
            CancelAll();
            for (int i = 0; i < notes.Count; i++)
            {
                ScheduledNote n = notes[i];
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = "ftc-" + i,
                    Title = n.Title,
                    Body = n.Body,
                    ThreadIdentifier = n.Thread,
                    ShowInForeground = false,
                    Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(Math.Max(1, n.WallUtc - nowWallUtc)), Repeats = false },
                });
            }
        }

        public void CancelAll()
        {
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
        }
    }
#endif
}
