using System;
using Game.Presentation.Art;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Ui
{
    /// <summary>
    /// The look of the UI (spec 008 §3.2, ADR 0038): warm cream and wood panels with thick rounded outlines, glossy buttons, a round
    /// friendly heading font (Baloo 2) and a plain body font (Noto Sans). All built in code (UI Toolkit styles), no UXML needed.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Cream = Painter.Hex(0xFFF3D1), CreamDark = Painter.Hex(0xF2DDA6), Wood = Painter.Hex(0x8A5A2B), WoodDark = Painter.Hex(0x4A3320);
        public static readonly Color WoodBar = Painter.Hex(0x6B4423), Gold = Painter.Hex(0xFFD968), Ink = Painter.Hex(0x4A3320);
        public static readonly Color Green = Painter.Hex(0x79C84A), GreenDark = Painter.Hex(0x3F7D20), Red = Painter.Hex(0xE8604C), RedDark = Painter.Hex(0x8F2B1E);
        public static readonly Color Sand = Painter.Hex(0xF3C777), SandDark = Painter.Hex(0x9A6A2A), Grey = Painter.Hex(0xC9C2B0), GreyDark = Painter.Hex(0x8A8372);
        public static readonly Color Warn = Painter.Hex(0xC2410C);

        private static Font _heading, _body, _bodyBold;

        public static Font Heading { get { return _heading ?? (_heading = Resources.Load<Font>("Fonts/Baloo2-Bold")); } }
        public static Font Body { get { return _body ?? (_body = Resources.Load<Font>("Fonts/NotoSans-Regular")); } }
        public static Font BodyBold { get { return _bodyBold ?? (_bodyBold = Resources.Load<Font>("Fonts/NotoSans-Bold")); } }

        public static void SetFont(VisualElement e, Font font) { e.style.unityFontDefinition = FontDefinition.FromFont(font); }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = w;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = c;
        }

        public static void Padding(VisualElement e, float v, float h)
        {
            e.style.paddingTop = e.style.paddingBottom = v;
            e.style.paddingLeft = e.style.paddingRight = h;
        }

        public static Label Text(string text, float size, Color colour, Font font = null, bool outline = false)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            SetFont(l, font ?? Body);
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.whiteSpace = WhiteSpace.Normal;
            // A short one-line text never splits ("Export play / log", "about / 16,850"): on a phone's fractional UI scale the
            // measured width can round a hair short, and the wrapped line then draws over the text below it. Labels made empty and
            // filled later (toasts, the dev panel) keep wrapping.
            if (!string.IsNullOrEmpty(text) && text.Length <= 28 && text.IndexOf('\n') < 0) { l.style.whiteSpace = WhiteSpace.NoWrap; l.style.flexShrink = 0; }
            if (outline) { l.style.unityTextOutlineWidth = Mathf.Max(2f, size / 14f); l.style.unityTextOutlineColor = WoodDark; }
            return l;
        }

        public static Label Heading1(string text, float size = 54f, Color? colour = null)
        {
            return Text(text, size, colour ?? Ink, Heading);
        }

        /// <summary>A cream panel with a wood outline and a soft shadow-like bottom edge.</summary>
        public static VisualElement Panel(float radius = 30f)
        {
            var p = new VisualElement();
            p.style.backgroundColor = Cream;
            Radius(p, radius);
            Border(p, 5, Wood);
            p.style.borderBottomWidth = 9;
            return p;
        }

        /// <summary>A dark wood pill for the HUD (cash, time), holding children in a row.</summary>
        public static VisualElement Pill()
        {
            var p = new VisualElement();
            p.style.flexDirection = FlexDirection.Row;
            p.style.alignItems = Align.Center;
            p.style.backgroundColor = WoodBar;
            Radius(p, 36);
            Border(p, 4, WoodDark);
            p.style.borderBottomWidth = 7;
            Padding(p, 8, 22);
            return p;
        }

        public static VisualElement Icon(Sprite sprite, float size)
        {
            var e = new VisualElement();
            e.pickingMode = PickingMode.Ignore;
            e.style.width = size;
            e.style.height = size;
            e.style.backgroundImage = new StyleBackground(sprite);
            e.style.flexShrink = 0;
            return e;
        }

        /// <summary>Called on every enabled button press (the tap sound, F08.5).</summary>
        public static Action OnPress { get; set; }

        public static Button MakeButton(string label, ActionLook look, Action onClick, string detail = null, bool enabled = true)
        {
            Action click = onClick;
            if (enabled && onClick != null) onClick = () => { if (OnPress != null) OnPress(); click(); };
            Color fill, edge, text = Color.white;
            switch (look)
            {
                case ActionLook.Danger: fill = Red; edge = RedDark; break;
                case ActionLook.Secondary: fill = Sand; edge = SandDark; text = Ink; break;
                default: fill = Green; edge = GreenDark; break;
            }
            if (!enabled) { fill = Grey; edge = GreyDark; text = Painter.Hex(0x6E6858); }
            var b = new Button(enabled ? onClick : null);
            b.text = "";
            b.style.backgroundColor = fill;
            b.style.minHeight = 84;
            b.style.minWidth = 260;
            b.style.marginRight = 14;
            b.style.marginTop = 10;
            b.style.flexDirection = FlexDirection.Column;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            Radius(b, 26);
            Border(b, 4, edge);
            b.style.borderBottomWidth = 9;
            Padding(b, 8, 26);
            b.style.overflow = Overflow.Hidden;

            // The gloss: a pale band across the top half.
            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.style.position = Position.Absolute;
            gloss.style.left = 6; gloss.style.right = 6; gloss.style.top = 4; gloss.style.height = Length.Percent(42);
            gloss.style.backgroundColor = new Color(1f, 1f, 1f, enabled ? 0.22f : 0.12f);
            Radius(gloss, 20);
            b.Add(gloss);

            b.Add(Text(label, 38, text, Heading, outline: enabled && look != ActionLook.Secondary));
            if (!string.IsNullOrEmpty(detail)) b.Add(Text(detail, 26, enabled && look != ActionLook.Secondary ? new Color(1f, 1f, 1f, 0.92f) : text, Body));
            ((Label)b[1]).style.unityTextAlign = TextAnchor.MiddleCenter;
            ((Label)b[1]).style.whiteSpace = WhiteSpace.NoWrap; // a title never splits on a phone's fractional UI scale
            if (b.childCount > 2) ((Label)b[2]).style.unityTextAlign = TextAnchor.MiddleCenter;

            if (enabled)
            {
                b.RegisterCallback<PointerDownEvent>(_ => b.style.scale = new Scale(new Vector3(0.95f, 0.95f, 1f)));
                b.RegisterCallback<PointerUpEvent>(_ => b.style.scale = new Scale(Vector3.one));
                b.RegisterCallback<PointerLeaveEvent>(_ => b.style.scale = new Scale(Vector3.one));
            }
            return b;
        }
    }

    public enum ActionLook
    {
        Primary = 0,
        Secondary = 1,
        Danger = 2,
    }
}
