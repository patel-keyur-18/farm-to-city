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

        /// <summary>Calls back true once per notification tap that opened the app (the play log's <c>fromNote</c>, ADR 0047 §2).</summary>
        void OpenedByNote(Action<bool> done);
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

        public void OpenedByNote(Action<bool> done) { done(false); }
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
                    Data = nowWallUtc + ":" + i, // unique per schedule, so a stale "last responded" isn't counted twice
                    Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(Math.Max(1, n.WallUtc - nowWallUtc)), Repeats = false },
                });
            }
        }

        public void OpenedByNote(Action<bool> done) { _host.StartCoroutine(Opened(done)); }

        private static IEnumerator Opened(Action<bool> done)
        {
            QueryLastRespondedNotificationOp op = iOSNotificationCenter.QueryLastRespondedNotification();
            float until = Time.realtimeSinceStartup + 2f;
            while (op.State == QueryLastRespondedNotificationState.Pending && Time.realtimeSinceStartup < until) yield return null;
            iOSNotification n = op.State == QueryLastRespondedNotificationState.HaveRespondedNotification ? op.Notification : null;
            // iOS keeps the last responded notification across launches: count each one once.
            bool fresh = n != null && !string.IsNullOrEmpty(n.Data) && n.Data != PlayerPrefs.GetString("note.lastOpened", "");
            if (fresh)
            {
                PlayerPrefs.SetString("note.lastOpened", n.Data);
                PlayerPrefs.Save();
            }
            done(fresh);
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
