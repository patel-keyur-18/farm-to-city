using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Game.Editor
{
    /// <summary>
    /// Plays touch input into the Editor's play mode, the way a phone sends it (STATUS.md, "Unity Editor quirks"): the mouse is
    /// removed (until the Editor restarts), a Touchscreen is added, and taps and drags are queued as TouchState events from
    /// InputSystem.onBeforeUpdate. Driven from the terminal with <c>unity command eval</c>, for example
    /// <c>Game.Editor.TouchDriver.Setup(); Game.Editor.TouchDriver.TapElement("people")</c>. Development tool only.
    /// </summary>
    public static class TouchDriver
    {
        private struct Step
        {
            public Vector2 Position;
            public TouchPhase Phase;
        }

        private static Touchscreen _touch;
        private static readonly Queue<Step> Steps = new Queue<Step>();
        private static readonly List<string> Log = new List<string>();

        public static string Setup()
        {
            if (Mouse.current != null) InputSystem.RemoveDevice(Mouse.current);
            _touch = Touchscreen.current ?? InputSystem.AddDevice<Touchscreen>();
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.onBeforeUpdate -= Pump;
            InputSystem.onBeforeUpdate += Pump;
            return "touch ready, mouse " + (Mouse.current == null ? "removed" : "present");
        }

        /// <summary>A tap at a screen point (pixels, y up): down, held a few frames, up.</summary>
        public static string Tap(float x, float y)
        {
            var p = new Vector2(x, y);
            Steps.Enqueue(new Step { Position = p, Phase = TouchPhase.Began });
            for (int i = 0; i < 3; i++) Steps.Enqueue(new Step { Position = p, Phase = TouchPhase.Stationary });
            Steps.Enqueue(new Step { Position = p, Phase = TouchPhase.Ended });
            return "tap queued at " + p;
        }

        /// <summary>A drag from one screen point to another in <paramref name="steps"/> moves.</summary>
        public static string Drag(float x0, float y0, float x1, float y1, int steps = 12)
        {
            Steps.Enqueue(new Step { Position = new Vector2(x0, y0), Phase = TouchPhase.Began });
            for (int i = 1; i <= steps; i++)
                Steps.Enqueue(new Step { Position = Vector2.Lerp(new Vector2(x0, y0), new Vector2(x1, y1), i / (float)steps), Phase = TouchPhase.Moved });
            Steps.Enqueue(new Step { Position = new Vector2(x1, y1), Phase = TouchPhase.Ended });
            return "drag queued";
        }

        /// <summary>
        /// Taps the centre of the first UI element whose text (or a child's) contains <paramref name="text"/>, converting its panel
        /// bounds to screen pixels. Returns where it aimed, or why it couldn't.
        /// </summary>
        public static string TapElement(string text)
        {
            UIDocument doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc == null) return "no UIDocument";
            VisualElement hit = Find(doc.rootVisualElement, text, true) ?? Find(doc.rootVisualElement, text, false);
            if (hit == null) return "no element with '" + text + "'";
            while (hit != null && !(hit is Button) && hit.pickingMode == PickingMode.Ignore) hit = hit.parent;
            if (hit == null) return "no tappable parent for '" + text + "'";
            ScrollView scroll = hit.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null) scroll.ScrollTo(hit);
            VisualElement target = hit;
            // After the scroll's layout settles, aim at the element's centre.
            target.schedule.Execute(() => Tap(ScreenOf(target).x, ScreenOf(target).y)).ExecuteLater(120);
            return "tap '" + text + "' (" + hit.GetType().Name + ")";
        }

        /// <summary>
        /// An element's centre in screen pixels (y up). In the Editor, Screen.height includes the Game view's toolbar, so it aims by
        /// the panel's own height (STATUS.md quirk).
        /// </summary>
        public static Vector2 ScreenOf(VisualElement e)
        {
            Rect r = e.worldBound;
            float k = e.panel.scaledPixelsPerPoint;
            float height = e.panel.visualTree.worldBound.height * k;
            return new Vector2(r.center.x * k, height - r.center.y * k);
        }

        /// <summary>The first ScrollView's vertical offset, to check that a finger drag scrolled it.</summary>
        public static float ScrollOffset()
        {
            UIDocument doc = Object.FindFirstObjectByType<UIDocument>();
            ScrollView s = doc == null ? null : doc.rootVisualElement.Q<ScrollView>();
            return s == null ? -1f : s.scrollOffset.y;
        }

        private static VisualElement Find(VisualElement e, string text, bool exact)
        {
            if (e is TextElement t && t.text != null && (exact ? t.text == text : t.text.Contains(text)) && e.resolvedStyle.display != DisplayStyle.None) return e;
            foreach (VisualElement c in e.Children())
            {
                VisualElement f = Find(c, text, exact);
                if (f != null) return f;
            }
            return null;
        }

        public static string Pending() { return Steps.Count + " steps queued; " + string.Join(" | ", Log); }

        private static void Pump()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || Steps.Count == 0 || _touch == null) return;
            Step s = Steps.Dequeue();
            InputSystem.QueueStateEvent(_touch, new TouchState { touchId = 1, phase = s.Phase, position = s.Position });
            if (s.Phase == TouchPhase.Began || s.Phase == TouchPhase.Ended)
            {
                Log.Add(s.Phase + "@" + s.Position);
                if (Log.Count > 8) Log.RemoveAt(0);
            }
        }
    }
}
