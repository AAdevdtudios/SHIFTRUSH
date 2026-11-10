using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShiftRush.EditorTools
{
    // =====================================================================================================
    //  SHIFT//RUSH Variable Finder
    //  Type part of a setting's name; every match on the selection (or the whole scene) is listed with its
    //  value, editable in place. ➜ selects the object and makes the field glow in the Inspector.
    //  Two ways in, sharing one search:
    //   - the Scene view overlay (dock it in the Scene toolbar next to the draw-mode menu, float it as a small
    //     panel, or hide it with the ` key / Overlays menu), and
    //   - the full window (SHIFT RUSH ▸ Variable Finder or Alt+F), dockable like any Unity tab.
    //  "speed.pull" searches only components whose name contains "speed".
    // =====================================================================================================

    /// <summary>The shared search: query, scope and results, plus drawing and the Inspector highlight.</summary>
    static class VariableSearch
    {
        const string PrefQuery = "ShiftRush.VariableFinder.Query";
        const string PrefScope = "ShiftRush.VariableFinder.Scope";
        public const int MaxResults = 300;

        public static readonly Color Magenta = new Color(1f, 0.16f, 0.72f);
        public static readonly Color Ink = new Color(0.09f, 0.08f, 0.11f);
        public static readonly Color Panel = new Color(0.13f, 0.12f, 0.16f);
        public static readonly Color RowAlt = new Color(0.155f, 0.145f, 0.19f);
        public static readonly Color Dim = new Color(0.62f, 0.6f, 0.68f);

        public enum Scope { Selection, Scene }

        public class Hit
        {
            public UnityEngine.Object owner;       // component or asset holding the field
            public GameObject gameObject;          // null for assets
            public SerializedObject so;
            public string path;
            public string label;
            public string ownerLabel;
        }

        public static readonly List<Hit> Hits = new List<Hit>();
        public static bool Truncated { get; private set; }
        /// <summary>Raised when the query, scope or results change, so every view repaints.</summary>
        public static event Action Changed;

        static string query;
        static Scope scope;
        static bool dirty = true;
        static double rebuildAt;

        static VariableSearch()
        {
            query = EditorPrefs.GetString(PrefQuery, "");
            scope = (Scope)EditorPrefs.GetInt(PrefScope, 0);
            Selection.selectionChanged += MarkDirty;
            EditorApplication.hierarchyChanged += MarkDirty;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (a, b) => MarkDirty();
            EditorApplication.update += Tick;
        }

        public static string Query
        {
            get { return query; }
            set
            {
                if (value == query) return;
                query = value; EditorPrefs.SetString(PrefQuery, query);
                rebuildAt = EditorApplication.timeSinceStartup + 0.15;   // debounce typing
                Notify();
            }
        }

        public static Scope CurrentScope
        {
            get { return scope; }
            set { if (value == scope) return; scope = value; EditorPrefs.SetInt(PrefScope, (int)scope); MarkDirty(); }
        }

        public static void MarkDirty() { dirty = true; Notify(); }
        static void Notify() { if (Changed != null) Changed(); }

        static void Tick()
        {
            if (rebuildAt > 0 && EditorApplication.timeSinceStartup >= rebuildAt) { rebuildAt = 0; MarkDirty(); }
        }

        /// <summary>Rebuilds results if needed. Call at the start of drawing.</summary>
        public static void Refresh()
        {
            if (dirty) Rebuild();
            Hits.RemoveAll(h => h.owner == null);
        }

        // ---------- search ----------

        static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.StartsWith("m_")) s = s.Substring(2);
            return new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        static void Rebuild()
        {
            dirty = false;
            Hits.Clear();
            Truncated = false;
            string compFilter = "", propFilter = query.Trim();
            int dot = propFilter.IndexOf('.');
            if (dot >= 0) { compFilter = Norm(propFilter.Substring(0, dot)); propFilter = propFilter.Substring(dot + 1); }
            propFilter = Norm(propFilter);
            if (propFilter.Length == 0 && compFilter.Length == 0) return;

            foreach (var owner in Owners())
            {
                if (owner == null) continue;
                string typeName = owner.GetType().Name;
                if (compFilter.Length > 0 && !Norm(typeName).Contains(compFilter)) continue;

                var so = new SerializedObject(owner);
                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = it.propertyType == SerializedPropertyType.Generic && !it.isArray;
                    if (it.propertyPath == "m_Script") continue;
                    if (propFilter.Length > 0 && !Norm(it.displayName).Contains(propFilter) && !Norm(it.name).Contains(propFilter)) continue;

                    var comp = owner as Component;
                    Hits.Add(new Hit
                    {
                        owner = owner,
                        gameObject = comp != null ? comp.gameObject : owner as GameObject,
                        so = so,
                        path = it.propertyPath,
                        label = it.displayName,
                        ownerLabel = ObjectNames.NicifyVariableName(typeName),
                    });
                    if (Hits.Count >= MaxResults) { Truncated = true; return; }
                }
            }
        }

        static IEnumerable<UnityEngine.Object> Owners()
        {
            IEnumerable<UnityEngine.Object> roots;
            if (scope == Scope.Scene)
                roots = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(g => (g.hideFlags & HideFlags.HideInHierarchy) == 0)
                    .OrderBy(HierarchyPath)
                    .Cast<UnityEngine.Object>();
            else
                roots = Selection.objects;

            foreach (var o in roots)
            {
                var go = o as GameObject;
                if (go == null) { yield return o; continue; }   // material, ScriptableObject, etc.
                foreach (var c in go.GetComponents<Component>())
                    if (c != null) yield return c;
            }
        }

        public static string HierarchyPath(GameObject g)
        {
            var t = g.transform; string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        public static string EmptyMessage(bool compact)
        {
            if (query.Trim().Length == 0)
                return compact ? "Type a setting name, like  shake"
                               : "Type part of a setting's name, like  pull  or  shake.\nUse  component.setting  to narrow it, like  speed.fov.";
            if (scope == Scope.Selection && Selection.objects.Length == 0)
                return compact ? "Nothing selected" : "Nothing selected. Select an object, or switch to Scene.";
            return "No setting matches \"" + query.Trim() + "\".";
        }

        // ---------- shared drawing ----------

        static GUIStyle tagStyle, ownerStyle, labelStyle, emptyStyle, goStyle, searchStyle;
        static Texture2D panelTex, altTex, magentaTex, inkTex;

        public static GUIStyle TagStyle { get { InitStyles(); return tagStyle; } }
        public static GUIStyle OwnerStyle { get { InitStyles(); return ownerStyle; } }
        public static GUIStyle EmptyStyle { get { InitStyles(); return emptyStyle; } }
        public static Texture2D InkTex { get { InitStyles(); return inkTex; } }
        public static Texture2D MagentaTex { get { InitStyles(); return magentaTex; } }

        static void InitStyles()
        {
            if (tagStyle != null && inkTex != null) return;
            inkTex = Tex(Ink); panelTex = Tex(Panel); altTex = Tex(RowAlt); magentaTex = Tex(Magenta);
            tagStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Magenta } };
            ownerStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Dim } };
            goStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = Magenta }, padding = new RectOffset(6, 0, 6, 2) };
            labelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.93f, 0.92f, 0.96f) } };
            emptyStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Dim } };
            searchStyle = EditorStyles.toolbarSearchField;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        static GUIContent expandContent;
        public static GUIContent ExpandContent
        {
            get
            {
                if (expandContent == null)
                    expandContent = new GUIContent(EditorGUIUtility.IconContent("d_ScaleTool").image, "Open the full Variable Finder (Alt+F)");
                return expandContent;
            }
        }

        /// <summary>Draws just the value, without the [Header]/[Space] decorations PropertyField would add.</summary>
        static bool DrawValue(SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float: prop.floatValue = EditorGUILayout.FloatField(prop.floatValue); break;
                case SerializedPropertyType.Integer: prop.intValue = EditorGUILayout.IntField(prop.intValue); break;
                case SerializedPropertyType.Boolean: prop.boolValue = EditorGUILayout.Toggle(prop.boolValue); break;
                case SerializedPropertyType.String: prop.stringValue = EditorGUILayout.TextField(prop.stringValue); break;
                case SerializedPropertyType.Color: prop.colorValue = EditorGUILayout.ColorField(prop.colorValue); break;
                case SerializedPropertyType.Vector2: prop.vector2Value = EditorGUILayout.Vector2Field(GUIContent.none, prop.vector2Value); break;
                case SerializedPropertyType.Vector3: prop.vector3Value = EditorGUILayout.Vector3Field(GUIContent.none, prop.vector3Value); break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = EditorGUILayout.Popup(prop.enumValueIndex, prop.enumDisplayNames); break;
                case SerializedPropertyType.ObjectReference:
                    EditorGUILayout.ObjectField(prop, GUIContent.none); break;
                default:
                    EditorGUILayout.PropertyField(prop, GUIContent.none, false); break;
            }
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>The search box.</summary>
        public static void SearchField(string controlName, params GUILayoutOption[] options)
        {
            InitStyles();
            GUI.SetNextControlName(controlName);
            Query = EditorGUILayout.TextField(query, searchStyle, options);
        }

        public static void ScopeToggle(float width)
        {
            CurrentScope = (Scope)GUILayout.Toolbar((int)scope, new[] { "Selection", "Scene" }, EditorStyles.miniButton, GUILayout.Width(width));
        }

        /// <summary>Draws result rows. maxRows limits the list (compact views); returns rows not shown.</summary>
        public static int DrawRows(float width, int maxRows, bool compact)
        {
            InitStyles();
            GameObject lastGo = null; UnityEngine.Object lastOwner = null; int row = 0, shown = 0;
            var updated = new HashSet<SerializedObject>();
            foreach (var h in Hits)
            {
                if (shown >= maxRows) break;
                if (updated.Add(h.so)) h.so.Update();
                var prop = h.so.FindProperty(h.path);
                if (prop == null) continue;

                if (!compact && (h.gameObject != lastGo || (h.gameObject == null && h.owner != lastOwner)))
                {
                    GUILayout.Label(h.gameObject != null ? HierarchyPath(h.gameObject) : h.owner.name, goStyle);
                    lastGo = h.gameObject;
                }
                lastOwner = h.owner;

                Rect r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(compact ? 20 : 22));
                GUI.DrawTexture(r, (row++ & 1) == 0 ? panelTex : altTex);
                GUILayout.Space(compact ? 4 : 10);
                using (new GUILayout.VerticalScope(GUILayout.Width(compact ? 150f : Mathf.Max(120f, width * 0.38f))))
                {
                    GUILayout.Label(h.label, labelStyle);
                    GUILayout.Label(compact && h.gameObject != null ? h.gameObject.name + " · " + h.ownerLabel : h.ownerLabel, ownerStyle);
                }
                using (new GUILayout.VerticalScope(GUILayout.MinWidth(40)))
                {
                    GUILayout.Space(3);
                    if (DrawValue(prop)) h.so.ApplyModifiedProperties();
                }
                if (GUILayout.Button(new GUIContent("➜", "Select it and highlight the field in the Inspector"), GUILayout.Width(26), GUILayout.Height(20)))
                    Reveal(h);
                GUILayout.Space(4);
                EditorGUILayout.EndHorizontal();
                shown++;
            }
            return Hits.Count - shown;
        }

        // ---------- reveal & highlight ----------

        enum UITK { Done, NotReady, IsIMGUI }

        /// <summary>How the last highlight went (for debugging).</summary>
        internal static string LastReveal = "";

        public static void Reveal(Hit h)
        {
            UnityEngine.Object target = h.gameObject != null ? (UnityEngine.Object)h.gameObject : h.owner;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);

            // Unfold the component and any parent foldouts so the field is on screen.
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(h.owner, true);
            var so = new SerializedObject(h.owner);
            string p = h.path;
            int cut;
            while ((cut = p.LastIndexOf('.')) > 0)
            {
                p = p.Substring(0, cut);
                var parent = so.FindProperty(p);
                if (parent != null) parent.isExpanded = true;
            }

            var inspector = FocusInspector();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            // The Inspector rebuilds over a couple of frames after a selection change.
            int tries = 0;
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (++tries < 3) return;
                var r = TryHighlightUIToolkit(inspector, h);
                bool done = r == UITK.Done
                         || (r == UITK.IsIMGUI && TryHighlightIMGUI(h))
                         || tries > 60;
                if (done) { EditorApplication.update -= step; LastReveal = r + " after " + tries + " ticks"; }
            };
            EditorApplication.update += step;
        }

        static EditorWindow FocusInspector()
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var existing = Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>().FirstOrDefault();
            var w = existing != null ? existing : EditorWindow.GetWindow(type);
            w.Show(); w.Focus();
            return w;
        }

        // Unity 6 draws default inspectors with UI Toolkit: find the PropertyField, scroll to it, make it glow.
        static UITK TryHighlightUIToolkit(EditorWindow inspector, Hit h)
        {
            if (inspector == null) return UITK.NotReady;
            InspectorElement host = null;
            foreach (var el in inspector.rootVisualElement.Query<InspectorElement>().ToList())
            {
                var ed = EditorOf(el);
                if (ed != null && ed.target == h.owner) { host = el; break; }
            }
            if (host == null) return UITK.NotReady;

            VisualElement field = host.Query<PropertyField>().Where(f => f.bindingPath == h.path).First();
            if (field == null)                          // custom IMGUI editor (e.g. RCC's): IMGUI highlighter instead
                return host.Query<IMGUIContainer>().First() != null ? UITK.IsIMGUI : UITK.NotReady;
            if (field.layout.height <= 0 || float.IsNaN(field.layout.height)) return UITK.NotReady;   // not laid out yet

            var sv = field.GetFirstAncestorOfType<ScrollView>();
            if (sv != null) sv.ScrollTo(field);
            Glow(field);
            return UITK.Done;
        }

        static Editor EditorOf(InspectorElement el)
        {
            // InspectorElement keeps its Editor in a non-public member; read it by reflection.
            var t = typeof(InspectorElement);
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var prop = t.GetProperty("editor", F);
            if (prop != null) return prop.GetValue(el) as Editor;
            var field = t.GetField("m_Editor", F);
            return field != null ? field.GetValue(el) as Editor : null;
        }

        static void Glow(VisualElement field)
        {
            var bg = field.style.backgroundColor;
            float start = (float)EditorApplication.timeSinceStartup;
            field.style.borderLeftWidth = 3;
            field.style.borderLeftColor = Magenta;
            field.style.borderTopLeftRadius = field.style.borderBottomLeftRadius = 2;
            IVisualElementScheduledItem item = null;
            item = field.schedule.Execute(() =>
            {
                float t = (float)EditorApplication.timeSinceStartup - start;
                if (t > 2.6f)
                {
                    field.style.backgroundColor = bg;
                    field.style.borderLeftWidth = StyleKeyword.Null;
                    field.style.borderLeftColor = StyleKeyword.Null;
                    item.Pause();
                    return;
                }
                float pulse = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2.2f);
                float fade = 1f - Mathf.Clamp01((t - 1.8f) / 0.8f);
                field.style.backgroundColor = new Color(Magenta.r, Magenta.g, Magenta.b, 0.32f * pulse * fade);
            }).Every(16);
        }

        // Custom inspectors (RCC's, for example) are drawn with IMGUI: use Unity's built-in highlighter.
        static bool TryHighlightIMGUI(Hit h)
        {
            bool found = Highlighter.Highlight("Inspector", h.path, HighlightSearchMode.Identifier)
                      || Highlighter.Highlight("Inspector", h.label, HighlightSearchMode.Auto);
            if (found)
            {
                double stopAt = EditorApplication.timeSinceStartup + 2.6;
                EditorApplication.CallbackFunction stop = null;
                stop = () =>
                {
                    if (EditorApplication.timeSinceStartup < stopAt) return;
                    Highlighter.Stop();
                    EditorApplication.update -= stop;
                };
                EditorApplication.update += stop;
            }
            return found;
        }
    }

    /// <summary>The full Variable Finder: a normal, dockable editor tab.</summary>
    public class VariableFinder : EditorWindow
    {
        Vector2 scroll;
        bool focusSearch;

        [MenuItem("SHIFT RUSH/Variable Finder &f")]
        public static void Open()
        {
            var w = GetWindow<VariableFinder>("Variable Finder");
            w.titleContent = new GUIContent("Variable Finder", EditorGUIUtility.IconContent("d_Search Icon").image);
            w.minSize = new Vector2(320, 200);
            w.Focus();
            w.focusSearch = true;
        }

        void OnEnable() { VariableSearch.Changed += Repaint; }
        void OnDisable() { VariableSearch.Changed -= Repaint; }

        void OnGUI()
        {
            GUI.DrawTexture(new Rect(0, 0, position.width, position.height), VariableSearch.InkTex);
            GUI.DrawTexture(new Rect(0, 0, 4, 52), VariableSearch.MagentaTex);

            GUILayout.Space(6);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                GUILayout.Label("SHIFT//RUSH  VARIABLE FINDER", VariableSearch.TagStyle);
                GUILayout.FlexibleSpace();
                VariableSearch.ScopeToggle(130);
                GUILayout.Space(8);
            }
            GUILayout.Space(4);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                VariableSearch.SearchField("VFSearchWindow");
                GUILayout.Space(8);
            }
            if (focusSearch) { EditorGUI.FocusTextInControl("VFSearchWindow"); focusSearch = false; }
            GUILayout.Space(8);

            VariableSearch.Refresh();
            if (VariableSearch.Hits.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(VariableSearch.EmptyMessage(false), VariableSearch.EmptyStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            scroll = GUILayout.BeginScrollView(scroll);
            VariableSearch.DrawRows(position.width, int.MaxValue, false);
            if (VariableSearch.Truncated)
                GUILayout.Label("Showing the first " + VariableSearch.MaxResults + " matches. Type more to narrow it down.", VariableSearch.OwnerStyle);
            GUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Scene view overlay. Docked in a Scene toolbar (e.g. next to the draw-mode menu) it is a slim search box
    /// plus an expand button; floating, it is a small panel with the top matches. Hide or show it from the
    /// Scene view's Overlays menu (the ` key).
    /// </summary>
    [Overlay(typeof(SceneView), Id, "Variable Finder", defaultDisplay = true)]
    public class VariableFinderOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar
    {
        const string Id = "shiftrush-variable-finder";
        const int CompactRows = 6;
        const float PanelWidth = 340f;

        public override void OnCreated()
        {
            VariableSearch.Changed += RepaintAll;
            collapsedIcon = EditorGUIUtility.IconContent("d_Search Icon").image as Texture2D;
        }

        [MenuItem("SHIFT RUSH/Show or Hide Scene Finder &#f")]
        static void ToggleInSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            Overlay o;
            if (sv.TryGetOverlay(Id, out o)) { o.displayed = !o.displayed; sv.Repaint(); }
        }
        public override void OnWillBeDestroyed() { VariableSearch.Changed -= RepaintAll; }

        readonly List<IMGUIContainer> views = new List<IMGUIContainer>();
        void RepaintAll() { foreach (var v in views) v.MarkDirtyRepaint(); }

        IMGUIContainer Track(IMGUIContainer c)
        {
            views.Add(c);
            c.RegisterCallback<DetachFromPanelEvent>(e => views.Remove(c));
            return c;
        }

        // Floating panel: search, scope, the first few matches, and an expand button.
        public override VisualElement CreatePanelContent()
        {
            var c = Track(new IMGUIContainer(DrawPanel));
            c.style.width = PanelWidth;
            return c;
        }

        void DrawPanel()
        {
            VariableSearch.Refresh();
            using (new GUILayout.HorizontalScope())
            {
                VariableSearch.SearchField("VFSearchOverlay");
                if (GUILayout.Button(VariableSearch.ExpandContent, EditorStyles.miniButton, GUILayout.Width(24)))
                    VariableFinder.Open();
            }
            GUILayout.Space(2);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                VariableSearch.ScopeToggle(120);
            }
            GUILayout.Space(2);

            if (VariableSearch.Hits.Count == 0)
            {
                GUILayout.Label(VariableSearch.EmptyMessage(true), VariableSearch.OwnerStyle);
                return;
            }
            int more = VariableSearch.DrawRows(PanelWidth, CompactRows, true);
            if (more > 0 && GUILayout.Button("+" + more + " more  ·  open full view", VariableSearch.OwnerStyle))
                VariableFinder.Open();
        }

        // Docked in a toolbar: just the search box and an expand button. Results open in the full view.
        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            var bar = new OverlayToolbar();
            var c = Track(new IMGUIContainer(DrawToolbar));
            c.style.width = 190;
            c.style.height = 20;
            c.style.marginTop = 1;
            bar.Add(c);
            return bar;
        }

        public OverlayToolbar CreateVerticalToolbarContent()
        {
            var bar = new OverlayToolbar();
            var btn = new EditorToolbarButton(VariableFinder.Open) { tooltip = "Variable Finder (Alt+F)" };
            btn.icon = EditorGUIUtility.IconContent("d_Search Icon").image as Texture2D;
            bar.Add(btn);
            return bar;
        }

        void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope())
            {
                VariableSearch.SearchField("VFSearchToolbar", GUILayout.Width(162));
                if (GUILayout.Button(VariableSearch.ExpandContent, EditorStyles.miniButton, GUILayout.Width(22)))
                    VariableFinder.Open();
                // Pressing Enter in the slim box opens the full view with the results.
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    && GUI.GetNameOfFocusedControl() == "VFSearchToolbar")
                {
                    VariableFinder.Open();
                    e.Use();
                }
            }
        }
    }
}
