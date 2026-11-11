using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed class SkillTreePanel : MonoBehaviour
    {
        private static readonly Color Gold = new Color(0.88f, 0.65f, 0.32f);
        private static readonly Rect UnlockButton = new Rect(915, 575, 300, 44);
        private const float UnlockHoldTime = 1f;
        private readonly Dictionary<string, Rect> positions = new Dictionary<string, Rect>();
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();
        private SkillCatalog catalog;
        private SkillProgress progress;
        private SkillCatalog.Node selected;
        private SkillCatalog.Node treeSelection;
        private string treeOwner;
        private bool treeGods;
        private SkillCatalog.Node[] visibleNodes;
        private string[] groups;
        private string owner, feedback;
        private SkillCatalog.Owner ownerProfile;
        private string ownerDetails;
        private bool gods, archive, confirmReset, buyPoints;
        private int buyCount, buyMaximum, buyDirection;
        private Vector2 lastDirection;
        private Vector2 lastPointer = new Vector2(float.NaN, float.NaN);
        private int archiveTab;
        private SkillCatalog.Item selectedItem;
        private SkillCatalog.Trophy selectedTrophy;
        private SkillCatalog.Item[] archiveItems;
        private static readonly string[] ArchiveTabs = { "技能", "战利品", "道具", "装备", "职业／神祇" };
        private const float ArchiveRowHeight = 45f;
        private const float ArchiveViewportHeight = 435f;
        private Vector2 scroll, growthScroll;
        private GUIStyle label;
        private GUIContent skillDetails;
        private Font font;
        private SkillCatalog.Node pendingUnlock;
        private float unlockStarted;
        private bool mouseUnlock;

        public void Open(SkillCatalog data, SkillProgress playerProgress, Font displayFont)
        {
            catalog = data;
            progress = playerProgress;
            font = displayFont;
            portraits.Clear();
            foreach (var entry in catalog.owners)
                portraits.Add(entry.group, LoadIcon(entry.portrait));
            archive = confirmReset = buyPoints = false;
            treeSelection = null;
            treeOwner = null;
            lastDirection = Vector2.zero;
            SelectTab(false);
            enabled = true;
        }

        private void SelectTab(bool selectGods)
        {
            gods = selectGods;
            string suffix = gods ? " [God]" : " [Class]";
            groups = catalog.owners.Select(entry => entry.group)
                .Where(group => group.EndsWith(suffix, StringComparison.Ordinal)).Distinct().ToArray();
            SelectOwner(groups[0]);
        }

        private void SelectOwner(string group)
        {
            pendingUnlock = null;
            owner = group;
            ownerProfile = Array.Find(catalog.owners, entry => entry.group == group);
            ownerDetails = ownerProfile.description + "\n\n" +
                string.Join("\n\n", ownerProfile.abilities.Select(ability => ability.title + "\n" + ability.description));
            growthScroll = Vector2.zero;
            visibleNodes = catalog.skills.Where(node => node.group == owner).ToArray();
            selected = null;
            feedback = null;
            scroll = Vector2.zero;
            positions.Clear();

            foreach (var node in visibleNodes)
            {
                positions.Add(node.id, new Rect(55 + (node.gridX - 1) * 115, 153 + (node.gridY - 1) * 108, 92, 88));
                LoadIcon(node.icon);
            }

            if (!archive && visibleNodes.Length > 0)
                SelectNode(visibleNodes[0]);
        }

        private Texture2D LoadIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (!icons.TryGetValue(key, out var texture))
            {
                texture = Resources.Load<Texture2D>("Jotunnslayer/Icons/" + key);
                icons.Add(key, texture);
            }

            return texture;
        }

        private void Update()
        {
            if (catalog == null)
                return;

            if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                GoBack();
                return;
            }

            if (confirmReset)
            {
                if (Input.GetButtonDown("Submit"))
                    ResetProgress();
                return;
            }

            if (buyPoints)
            {
                float horizontal = Input.GetAxisRaw("Horizontal");
                int adjustment = horizontal < -0.5f ? -1 : horizontal > 0.5f ? 1 : 0;
                if (buyMaximum > 0 && adjustment != 0 && adjustment != buyDirection)
                    buyCount = Mathf.Clamp(buyCount + adjustment, 1, buyMaximum);

                buyDirection = adjustment;
                if (Input.GetButtonDown("Submit"))
                    PurchasePoints();
                return;
            }

            if (Input.GetKeyDown(KeyCode.PageUp))
                growthScroll.y -= 280f;
            if (Input.GetKeyDown(KeyCode.PageDown))
                growthScroll.y += 280f;

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.JoystickButton6))
            {
                SetArchive(!archive);
                return;
            }

            if (archive)
            {
                UpdateArchiveNavigation();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton2))
            {
                OpenPointPurchase();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton3))
                SelectTab(!gods);

            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4))
                MoveOwner(-1);
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5))
                MoveOwner(1);

            var raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector2 direction = raw.sqrMagnitude < 0.25f ? Vector2.zero :
                Mathf.Abs(raw.x) >= Mathf.Abs(raw.y) ? new Vector2(Mathf.Sign(raw.x), 0) :
                new Vector2(0, -Mathf.Sign(raw.y));
            if (direction != Vector2.zero && direction != lastDirection)
                MoveFocus(direction);

            lastDirection = direction;
            if (Input.GetButtonDown("Submit"))
                BeginUnlock(false, Time.unscaledTime);

            if (pendingUnlock == null)
                return;

            bool held = mouseUnlock
                ? Input.GetMouseButton(0) && UnlockButton.Contains(GetPanelPointer())
                : Input.GetButton("Submit");
            UpdateUnlock(held, Time.unscaledTime);
        }

        internal void UpdateUnlock(bool held, float now)
        {
            if (pendingUnlock == null)
                return;

            if (!held)
            {
                pendingUnlock = null;
                return;
            }

            if (now - unlockStarted >= UnlockHoldTime)
            {
                var node = pendingUnlock;
                pendingUnlock = null;
                feedback = progress.TryLearn(node.id) ? "已解锁" : GetLockReason(node);
            }
        }

        private void MoveOwner(int offset)
        {
            int index = Array.IndexOf(groups, owner);
            SelectOwner(groups[(index + offset + groups.Length) % groups.Length]);
        }

        private void MoveFocus(Vector2 direction)
        {
            if (selected == null)
            {
                SelectNode(visibleNodes[0]);
                return;
            }

            Vector2 origin = positions[selected.id].center;
            SkillCatalog.Node next = null;
            float best = float.PositiveInfinity;
            foreach (var node in visibleNodes)
            {
                Vector2 delta = positions[node.id].center - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 0)
                    continue;

                float lateral = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                float score = forward + lateral * 2;
                if (score < best)
                {
                    best = score;
                    next = node;
                }
            }

            if (next != null)
                SelectNode(next);
        }

        internal void BeginUnlock(bool withMouse, float now)
        {
            if (!isActiveAndEnabled || archive || confirmReset || buyPoints || pendingUnlock != null ||
                selected == null || !progress.CanLearn(selected.id))
                return;

            pendingUnlock = selected;
            mouseUnlock = withMouse;
            unlockStarted = now;
        }

        private void GoBack()
        {
            pendingUnlock = null;
            if (confirmReset) confirmReset = false;
            else if (buyPoints) buyPoints = false;
            else if (archive) SetArchive(false);
            else enabled = false;
        }

        internal void SetArchive(bool open)
        {
            if (archive == open)
                return;
            pendingUnlock = null;
            if (open)
            {
                treeSelection = selected;
                treeOwner = owner;
                treeGods = gods;
            }
            archive = open;
            scroll = Vector2.zero;
            lastDirection = Vector2.zero;
            if (open)
                return;

            if (gods != treeGods)
                SelectTab(treeGods);
            if (owner != treeOwner)
                SelectOwner(treeOwner);
            if (treeSelection != null)
                SelectNode(treeSelection);
            else if (visibleNodes.Length > 0)
                SelectNode(visibleNodes[0]);
            treeSelection = null;
            treeOwner = null;
        }

        private void UpdateArchiveNavigation()
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4))
                SelectArchiveTab((archiveTab + ArchiveTabs.Length - 1) % ArchiveTabs.Length);
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5))
                SelectArchiveTab((archiveTab + 1) % ArchiveTabs.Length);
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton3))
                SelectTab(!gods);

            bool owners = archiveTab == 4;
            float axis = owners ? Input.GetAxisRaw("Horizontal") : -Input.GetAxisRaw("Vertical");
            Vector2 direction = Mathf.Abs(axis) < .5f ? Vector2.zero :
                (owners ? Vector2.right : Vector2.up) * Mathf.Sign(axis);
            if (direction != Vector2.zero && direction != lastDirection)
            {
                if (owners) MoveOwner((int)direction.x);
                else MoveArchiveSelection((int)direction.y);
            }
            lastDirection = direction;
        }

        internal void SelectArchiveTab(int tab)
        {
            if (archiveTab == tab)
                return;
            archiveTab = tab;
            lastDirection = Vector2.zero;
            scroll = growthScroll = Vector2.zero;
            selected = null;
            selectedItem = null;
            selectedTrophy = null;
            if (tab == 2 || tab == 3)
                archiveItems = catalog.items.Where(item => item.equipment == (tab == 3)).ToArray();
        }

        internal void MoveArchiveSelection(int offset)
        {
            int count = ArchiveRowCount();
            if (count == 0)
                return;

            int index = Mathf.Clamp(SelectedArchiveRow() + offset, 0, count - 1);
            SelectArchiveRow(index);
            float top = index * ArchiveRowHeight;
            scroll.y = Mathf.Clamp(scroll.y, Mathf.Max(0f, top + ArchiveRowHeight - ArchiveViewportHeight), top);
        }

        private int ArchiveRowCount() => archiveTab == 0 ? visibleNodes.Length :
            archiveTab == 1 ? catalog.awards.Length : archiveTab == 4 ? 0 : archiveItems.Length;

        internal float ArchiveScrollOffset => scroll.y;
        internal SkillCatalog.Node SelectedNode => selected;

        internal int SelectedArchiveRow() => archiveTab == 0 ? Array.IndexOf(visibleNodes, selected) :
            archiveTab == 1 ? Array.IndexOf(catalog.awards, selectedTrophy) : Array.IndexOf(archiveItems, selectedItem);

        private void SelectArchiveRow(int index)
        {
            if (archiveTab == 0)
                SelectNode(visibleNodes[index]);
            else if (archiveTab == 1)
                selectedTrophy = catalog.awards[index];
            else
                selectedItem = archiveItems[index];
            growthScroll = Vector2.zero;
        }

        private void OnDisable() => pendingUnlock = null;

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                pendingUnlock = null;
        }

        private static float PanelScale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        private static Vector2 PanelOffset(float scale) =>
            new Vector2((Screen.width - 1280 * scale) * 0.5f, (Screen.height - 720 * scale) * 0.5f);

        private static Vector2 GetPanelPointer()
        {
            Vector3 pointer = Input.mousePosition;
            float scale = PanelScale;
            return (new Vector2(pointer.x, Screen.height - pointer.y) - PanelOffset(scale)) / scale;
        }

        private void ResetProgress()
        {
            int before = progress.Points;
            progress.Reset();
            confirmReset = false;
            feedback = $"已重置，返还 {progress.Points - before} 点";
        }

        private void OpenPointPurchase()
        {
            buyMaximum = progress.MaxBuyablePoints();
            if (buyMaximum == 0)
                return;

            pendingUnlock = null;
            buyDirection = 0;
            buyCount = buyMaximum;
            buyPoints = true;
        }

        private void PurchasePoints()
        {
            if (progress.TryBuyPoints(buyCount))
            {
                feedback = $"已兑换 {buyCount} 技能点";
                buyMaximum = progress.MaxBuyablePoints();
                buyCount = buyMaximum;
            }
        }

        private void DrawPointPurchase()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.7f));
            GUI.Box(new Rect(410, 229, 460, 264), GUIContent.none);
            GUI.Label(new Rect(450, 251, 380, 38), $"兑换技能点    持有 {progress.Gold} 金币", label);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && buyMaximum > 1;
            if (GUI.Button(new Rect(450, 305, 38, 32), "−"))
                buyCount = Mathf.Max(1, buyCount - 1);
            if (buyMaximum > 1)
                buyCount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(505, 315, 270, 24), buyCount, 1, buyMaximum));
            if (GUI.Button(new Rect(792, 305, 38, 32), "+"))
                buyCount = Mathf.Min(buyMaximum, buyCount + 1);

            GUI.Label(new Rect(450, 351, 380, 48), $"{buyCount} 技能点    共 {progress.QuotePoints(buyCount)} 金币", label);
            GUI.enabled = wasEnabled && buyCount > 0;
            if (GUI.Button(new Rect(450, 415, 174, 44), "确认兑换"))
                PurchasePoints();
            GUI.enabled = wasEnabled;
            if (GUI.Button(new Rect(656, 415, 174, 44), "返回"))
                GoBack();
        }

        private void OnGUI()
        {
            if (catalog == null)
                return;

            float scale = PanelScale;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(PanelOffset(scale), Quaternion.identity, new Vector3(scale, scale, 1));

            label ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18, richText = true };
            if (font)
                label.font = font;

            Fill(new Rect(0, 0, 1280, 720), new Color(0.035f, 0.046f, 0.055f));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !confirmReset && !buyPoints;
            if (GUI.Button(new Rect(450, 16, 115, 34), "职业")) SelectTab(false);
            if (GUI.Button(new Rect(580, 16, 115, 34), "神祇")) SelectTab(true);
            if (GUI.Button(new Rect(1050, 16, 160, 34), archive ? "技能树 · F1" : "图鉴 · F1"))
                SetArchive(!archive);

            for (int i = 0; i < groups.Length; i++)
            {
                string group = groups[i];
                GUI.color = group == owner ? Gold : Color.white;
                var portrait = new Rect(45 + i * 142, 57, 130, 63);
                if (GUI.Button(portrait, GUIContent.none))
                    SelectOwner(group);

                if (portraits.TryGetValue(group, out var texture) && texture)
                    GUI.DrawTexture(portrait, texture, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(portrait.x, portrait.yMax, portrait.width, 26), Array.Find(catalog.owners, entry => entry.group == group).title, label);
            }

            GUI.color = Color.white;
            if (archive) DrawArchive();
            else
            {
                Vector2 pointer = Event.current.mousePosition;
                bool pointerMoved = Event.current.type == EventType.Repaint && !pointer.Equals(lastPointer);
                if (pointerMoved)
                    lastPointer = pointer;
                DrawTree(pointerMoved);
            }
            DrawDetails();

            Fill(new Rect(40, 640, 1200, 1), Gold);
            GUI.Label(new Rect(375, 652, 355, 50), $"可用技能点 {progress.Points} ◆    金币 {progress.Gold}", label);
            if (!archive)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && progress.Gold >= progress.NextPointPrice;
                if (GUI.Button(new Rect(750, 653, 230, 40), $"兑换技能点 · {progress.NextPointPrice} 金币起"))
                    OpenPointPurchase();
                GUI.enabled = wasEnabled;
            }
            if (!archive && GUI.Button(new Rect(1000, 653, 104, 40), "重置"))
            {
                pendingUnlock = null;
                confirmReset = true;
            }
            if (GUI.Button(new Rect(1120, 653, 104, 40), "返回"))
                GoBack();

            GUI.enabled = previousEnabled;
            if (confirmReset)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 0.7f));
                GUI.Box(new Rect(430, 270, 420, 170), "");
                GUI.Label(new Rect(466, 298, 355, 52), "重置已学习节点并返还技能点？", label);
                if (GUI.Button(new Rect(475, 365, 145, 44), "确认重置")) ResetProgress();
                if (GUI.Button(new Rect(655, 365, 145, 44), "取消")) confirmReset = false;
            }
            else if (buyPoints)
            {
                DrawPointPurchase();
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawTree(bool pointerMoved)
        {
            foreach (var node in visibleNodes)
            {
                Rect child = positions[node.id];
                foreach (string prerequisite in node.conditions)
                {
                    if (positions.TryGetValue(prerequisite, out var parent))
                        DrawLine(parent.center, child.center, progress.IsLearned(prerequisite) ? Gold : Color.gray);
                }
            }

            foreach (var node in visibleNodes)
            {
                bool learned = progress.IsLearned(node.id);
                GUI.color = learned ? Gold : progress.CanLearn(node.id) ? Color.white : Color.gray;
                Rect target = positions[node.id];
                if (!confirmReset && !buyPoints && pointerMoved && target.Contains(lastPointer))
                {
                    SelectNode(node);
                }

                if (GUI.Button(target, GUIContent.none))
                    SelectNode(node);

                if (selected == node)
                    Fill(new Rect(target.x, target.yMax - 3, target.width, 3), Gold);

                Rect rect = positions[node.id];
                var iconRect = new Rect(rect.x + 10, rect.y + 2, 72, 60);
                if (!string.IsNullOrEmpty(node.icon) && icons.TryGetValue(node.icon, out var texture) && texture)
                    GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(iconRect, node.title, label);

                GUI.Label(new Rect(rect.x + 24, rect.y + 60, 65, 27), learned ? "✓" : $"{node.cost} ◆", label);
            }

            GUI.color = Color.white;
        }

        private void DrawArchive()
        {
            for (int tab = 0; tab < ArchiveTabs.Length; tab++)
            {
                if (GUI.Button(new Rect(50 + tab * 135, 135, 125, 35), ArchiveTabs[tab]))
                    SelectArchiveTab(tab);
            }

            if (archiveTab == 4)
            {
                if (portraits.TryGetValue(owner, out var portrait) && portrait)
                    GUI.DrawTexture(new Rect(55, 200, 300, 300), portrait, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(390, 200, 460, 80), ownerProfile.title, label);
                return;
            }

            int count = ArchiveRowCount();
            scroll = GUI.BeginScrollView(new Rect(50, 185, 820, ArchiveViewportHeight), scroll,
                new Rect(0, 0, 790, count * ArchiveRowHeight));
            int first = Mathf.Clamp(Mathf.FloorToInt(scroll.y / ArchiveRowHeight), 0, count);
            int end = Mathf.Min(first + 11, count);
            int selectedRow = SelectedArchiveRow();
            Color background = GUI.backgroundColor;
            for (int i = first; i < end; i++)
            {
                var rect = new Rect(0, i * ArchiveRowHeight, 770, ArchiveRowHeight - 3);
                string title = archiveTab == 0 ? visibleNodes[i].title :
                    archiveTab == 1 ? catalog.awards[i].title : archiveItems[i].title;
                GUI.backgroundColor = i == selectedRow ? Gold : background;
                if (GUI.Button(rect, title))
                    SelectArchiveRow(i);
            }
            GUI.backgroundColor = background;

            GUI.EndScrollView();
        }

        private void DrawDetails()
        {
            if (archive && archiveTab != 0)
            {
                DrawArchiveDetails();
                return;
            }

            if (selected == null)
            {
                if (!archive && feedback != null)
                    GUI.Label(new Rect(915, 510, 300, 85), feedback, label);
                return;
            }

            DrawDetailScroll(skillDetails, archive ? 455 : 350);
            if (!archive)
            {
                GUI.Label(new Rect(915, 510, 300, 58), feedback ?? GetLockReason(selected), label);
                DrawUnlockButton();
            }
        }

        private void DrawUnlockButton()
        {
            bool canLearn = progress.CanLearn(selected.id);
            GUI.Box(UnlockButton, GUIContent.none);
            if (pendingUnlock != null)
            {
                float fraction = Mathf.Clamp01((Time.unscaledTime - unlockStarted) / UnlockHoldTime);
                Fill(new Rect(UnlockButton.x, UnlockButton.yMax - 4, UnlockButton.width * fraction, 4), Gold);
            }

            GUI.Label(new Rect(UnlockButton.x + 12, UnlockButton.y + 8, UnlockButton.width - 24, 32),
                canLearn ? $"长按解锁 · {selected.cost} 技能点" : GetLockReason(selected), label);
            var evt = Event.current;
            if (GUI.enabled && canLearn && evt.type == EventType.MouseDown && evt.button == 0 &&
                UnlockButton.Contains(evt.mousePosition))
            {
                BeginUnlock(true, Time.unscaledTime);
                evt.Use();
            }
        }

        private void DrawArchiveDetails()
        {
            if (archiveTab == 1 && selectedTrophy != null)
            {
                var trophy = selectedTrophy;
                DrawArchiveText($"{trophy.title}\n\n{trophy.desc}\n\n" +
                    trophy.condition.Replace("{0}", trophy.count.ToString()) + $"\n\n奖励 {trophy.reward}");
            }
            else if (archiveTab == 4)
            {
                DrawArchiveText(ownerDetails);
            }
            else if (archiveTab >= 2 && selectedItem != null)
            {
                var item = selectedItem;
                string mask = item.characterMask == 0 ? "" : $"\n适用角色掩码 {item.characterMask}";
                string details = $"{item.title}\n{item.category}   {item.owner}\n\n{item.description}\n\n" +
                    $"{item.skill}\n{item.skillDescription}\n";
                if (item.effects != null)
                    details += string.Join("\n", item.effects);

                DrawArchiveText(details + $"\n\n{item.id}{mask}\n{item.asset}");
            }
        }

        private void DrawArchiveText(string value)
        {
            DrawDetailScroll(new GUIContent(value), 455);
        }

        private void DrawDetailScroll(GUIContent content, float viewportHeight)
        {
            float height = label.CalcHeight(content, 271);
            growthScroll.y = Mathf.Clamp(growthScroll.y, 0f, Mathf.Max(0f, height - viewportHeight));
            growthScroll = GUI.BeginScrollView(new Rect(915, 152, 300, viewportHeight), growthScroll,
                new Rect(0, 0, 275, height));
            GUI.Label(new Rect(0, 0, 271, height), content, label);
            GUI.EndScrollView();
        }

        internal void SelectNode(SkillCatalog.Node node)
        {
            if (selected == node)
                return;

            pendingUnlock = null;
            selected = node;
            feedback = null;
            growthScroll = Vector2.zero;
            string details = $"{node.title}\n\n{node.desc}\n\n技能点 {node.cost}";
            if (node.souls > 0)
                details += $"   魂石 {node.souls}";
            if (node.conditions.Length > 0)
            {
                var requirements = node.conditions.Select(id => Array.Find(catalog.skills, skill => skill.id == id).title);
                details += "\n前置 " + string.Join("、", requirements);
            }

            if (node.growth.Length > 0)
            {
                details += "\n\n成长效果\n\n" + string.Join("\n\n", node.growth.Select(level =>
                    $"{level.title}\n{string.Join("\n", level.effects)}"));
            }

            skillDetails = new GUIContent(details);
        }

        private string GetLockReason(SkillCatalog.Node node)
        {
            if (progress.IsLearned(node.id))
                return "已学习";
            foreach (string prerequisite in node.conditions)
            {
                if (!progress.IsLearned(prerequisite))
                    return "需要先学习 " + Array.Find(catalog.skills, skill => skill.id == prerequisite).title;
            }

            if (node.souls > 0)
                return "需要魂石";
            return progress.CanLearn(node.id) ? "按住解锁按钮或确认键 1 秒" : "技能点不足";
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Matrix4x4 old = GUI.matrix;
            Vector2 direction = end - start;
            GUIUtility.RotateAroundPivot(Vector2.SignedAngle(Vector2.right, direction), start);
            Fill(new Rect(start.x, start.y, direction.magnitude, 2), color);
            GUI.matrix = old;
        }
    }
}







