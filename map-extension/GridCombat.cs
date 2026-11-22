using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapExtension
{
    // ===== 간단한 턴제 전투 (격자 수색의 적 칸) =====
    // 나 vs 적 무리. 내 차례에 [공격 / 방어 / 치료 / 도주] 중 하나 → 살아 있는 적이 차례로 공격.
    //  - 체력: 시작 = HUD 체력 (10 - 부상 수치) × 10.
    //    끝나고 잃은 체력이 30 이상이면 경상, 쓰러지면 중상 (게임 부상으로 반영).
    //  - 무기: 외출 가방(바닥 + 배낭 + 주머니)에서 제일 센 것. 총은 탄약(AMMUNITION)이 있어야 하고 한 발마다 1개 쓴다.
    //    날붙이 = 출혈(3턴), 둔기 = 기절(30%). 없으면 맨주먹.
    //  - 방어구(ARMOR)가 있으면 받는 피해 -30%. [방어] 한 턴은 받는 피해 -60%.
    //  - 치료: 의약품(MEDICAL) 1개 쓰고 회복 (붕대 15, 나머지 25).
    //  - 도주: 60%. 실패하면 적이 공격. 도망치면 적은 그 칸에 남는다.
    internal static class GridCombat
    {
        internal enum Result { Won, Lost, Fled }

        private class Foe
        {
            public string Name;
            public int Hp, MaxHp, DmgMin, DmgMax, Acc;
            public int BleedTurns;
            public bool Stunned;
            public bool Dead => Hp <= 0;
        }

        private class Weapon
        {
            public string Name;
            public int DmgMin, DmgMax, Acc, Shots = 1;
            public bool Gun, Bleed, Stun;
        }

        // 적 무리 (GridFarmLocation.EnemyGroups 번호)
        private static List<Foe> Group(int g)
        {
            Foe Thug() => new Foe { Name = "깡패", MaxHp = 25, DmgMin = 4, DmgMax = 8, Acc = 65 };
            Foe Raider() => new Foe { Name = "약탈자", MaxHp = 35, DmgMin = 6, DmgMax = 10, Acc = 70 };
            Foe Boss() => new Foe { Name = "깡패 두목", MaxHp = 50, DmgMin = 8, DmgMax = 13, Acc = 70 };
            var list = g == 0 ? new List<Foe> { Thug(), Thug() }
                     : g == 1 ? new List<Foe> { Thug(), Raider() }
                     : new List<Foe> { Raider(), Raider(), Boss() };
            // 같은 이름이 여럿이면 A, B, ...
            var total = new Dictionary<string, int>();
            foreach (Foe f in list) total[f.Name] = (total.TryGetValue(f.Name, out int n) ? n : 0) + 1;
            var seen = new Dictionary<string, int>();
            foreach (Foe f in list)
            {
                f.Hp = f.MaxHp;
                if (total[f.Name] < 2) continue;
                int k = seen.TryGetValue(f.Name, out int s) ? s : 0;
                seen[f.Name] = k + 1;
                f.Name += " " + (char)('A' + k);
            }
            return list;
        }

        private static List<Foe> foes;
        private static int hp, startHp, target;
        private static bool defending, over;
        private static Action<Result> onEnd;
        private static readonly List<string> log = new List<string>();

        // ---- 시작 ----
        internal static void Start(int group, Action<Result> done)
        {
            Close();
            foes = Group(group);
            onEnd = done;
            HealthData hd = PlayerStore.Instance.healthData;
            startHp = hp = Mathf.Clamp((10 - hd.woundState) * 10, 10, 100);
            target = 0;
            over = false;
            log.Clear();
            Log("<color=#ff8a6b>" + string.Join(", ", foes.ConvertAll(f => f.Name)) + "이(가) 길을 막는다!</color>");
            Weapon w = BestWeapon();
            Log("무기: " + w.Name + (w.Gun ? " (탄약 " + AmmoCount() + ")" : ""));
            try
            {
                Build();
                Refresh();
                Debug.Log("[MapExtension] combat start: group " + group + ", " + foes.Count + " foe(s), hp " + hp + ", weapon " + w.Name);
            }
            catch (Exception ex)
            {
                // 창을 못 만들면 어두운 판만 남아 화면이 막히지 않게 바로 닫고 "도망침"으로
                Debug.LogWarning("[MapExtension] combat window: " + ex);
                Close();
                Action<Result> cb = onEnd;
                onEnd = null;
                cb?.Invoke(Result.Fled);
            }
        }

        internal static bool IsOpen => root != null;

        // Esc: 끝났으면 닫기, 아니면 도주 시도 (창이 이상해도 빠져나갈 수 있게)
        private class EscWatcher : MonoBehaviour
        {
            private void Update()
            {
                if (!Input.GetKeyDown(KeyCode.Escape)) return;
                if (over) Finish();
                else Act("flee");
            }
        }

        // ---- 내 차례 ----
        private static void Act(string what)
        {
            if (over) return;
            defending = false;
            switch (what)
            {
                case "attack": Attack(); break;
                case "defend": defending = true; Log("몸을 웅크려 방어 자세를 취했다."); break;
                case "heal":
                    if (!Heal()) { Refresh(); return; } // 약이 없으면 턴을 안 쓴다
                    break;
                case "flee":
                    if (RNG.Roll(60f)) { Log("도망쳤다!"); End(Result.Fled); return; }
                    Log("<color=#ff8a6b>도망치지 못했다!</color>");
                    break;
            }
            if (foes.TrueForAll(f => f.Dead)) { Log("<color=#4df0e0>적을 모두 쓰러뜨렸다!</color>"); End(Result.Won); return; }
            EnemyTurn();
            if (hp <= 0) { Log("<color=#ff5a5a>쓰러졌다…</color>"); End(Result.Lost); return; }
            Refresh();
        }

        private static void Attack()
        {
            if (target < 0 || target >= foes.Count || foes[target].Dead) target = foes.FindIndex(f => !f.Dead);
            if (target < 0) return;
            Foe t = foes[target];
            Weapon w = BestWeapon();
            for (int s = 0; s < w.Shots && !t.Dead; s++)
            {
                if (w.Gun && !UseAmmo()) { Log("<color=#ff8a6b>탄약이 떨어졌다!</color>"); break; }
                try { AudioManager.Instance.Play2(w.Gun ? "gun_shot" : "debris_impact2"); } catch { }
                if (!RNG.Roll(w.Acc)) { Log(w.Name + " 공격이 빗나갔다."); continue; }
                int dmg = UnityEngine.Random.Range(w.DmgMin, w.DmgMax + 1);
                bool crit = RNG.Roll(10f);
                if (crit) dmg = Mathf.RoundToInt(dmg * 1.5f);
                t.Hp = Mathf.Max(0, t.Hp - dmg);
                string extra = "";
                if (!t.Dead && w.Bleed) { t.BleedTurns = 3; extra = " <color=#ff5a5a>출혈!</color>"; }
                if (!t.Dead && w.Stun && RNG.Roll(30f)) { t.Stunned = true; extra += " <color=#ffcf6b>기절!</color>"; }
                Log(t.Name + "에게 " + dmg + " 피해" + (crit ? " <color=#ffcf6b>(치명타)</color>" : "") + extra + (t.Dead ? " → 쓰러짐" : ""));
            }
        }

        private static bool Heal()
        {
            GameItem med = FindItem("MEDICAL");
            if (med == null) { Log("<color=#9a9aa8>쓸 수 있는 의약품이 없다.</color>"); return false; }
            string name = Name(med);
            int amount = med.identifier.Contains("bandage") ? 15 : 25;
            ConsumeOne(med);
            int before = hp;
            hp = Mathf.Min(startHp, hp + amount); // 전투 시작 체력까지만
            Log(name + " 사용: 체력 +" + (hp - before));
            return true;
        }

        private static void EnemyTurn()
        {
            bool armor = FindItem("ARMOR") != null;
            foreach (Foe f in foes)
            {
                if (f.Dead) continue;
                if (f.BleedTurns > 0)
                {
                    f.BleedTurns--;
                    f.Hp = Mathf.Max(0, f.Hp - 3);
                    Log(f.Name + " 출혈 3" + (f.Dead ? " → 쓰러짐" : ""));
                    if (f.Dead) continue;
                }
                if (f.Stunned) { f.Stunned = false; Log(f.Name + "은(는) 정신을 못 차린다."); continue; }
                if (!RNG.Roll(f.Acc)) { Log(f.Name + "의 공격을 피했다."); continue; }
                float dmg = UnityEngine.Random.Range(f.DmgMin, f.DmgMax + 1);
                if (armor) dmg *= 0.7f;
                if (defending) dmg *= 0.4f;
                int d = Mathf.Max(1, Mathf.RoundToInt(dmg));
                hp = Mathf.Max(0, hp - d);
                try { AudioManager.Instance.Play2("debris_impact"); } catch { }
                Log("<color=#ff8a6b>" + f.Name + "에게 " + d + " 피해를 입었다.</color>");
                if (hp <= 0) return;
            }
        }

        private static void End(Result r)
        {
            over = true;
            int lost = startHp - hp;
            Debug.Log("[MapExtension] combat end: " + r + ", hp " + hp + "/" + startHp + " (lost " + lost + ")");
            try
            {
                if (r == Result.Lost) HealthData.ReceiveMajorWound();
                else if (lost >= 30) HealthData.ReceiveMinorWound();
            }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] combat wound: " + ex.Message); }
            string msg = r == Result.Won ? "이겼다." : r == Result.Fled ? "도망쳤다. 적은 아직 그 자리에 있다." : "쓰러졌다… 간신히 기어서 빠져나왔다. 오늘 수색은 여기까지.";
            if (r != Result.Lost && lost >= 30) msg += " (부상)";
            Refresh();
            // 결과는 [닫기] 버튼으로 확인 후 반영
            SetButtons(false);
            if (closeBtn != null) closeBtn.SetActive(true);
            pending = r;
            GridFarmLocation.Notify(msg);
        }

        private static Result pending;

        private static void Finish()
        {
            Action<Result> cb = onEnd;
            onEnd = null;
            Close();
            try { cb?.Invoke(pending); } catch (Exception ex) { Debug.LogWarning("[MapExtension] combat end: " + ex); }
        }

        private static void Log(string s)
        {
            log.Add(s);
            while (log.Count > 7) log.RemoveAt(0);
        }

        // ---- 가방 속 장비 ----
        private static List<GameItem> Carried()
        {
            try { return EmporiumEntry.Instance.GetAllAfterhourOwnedItems(); } catch { return new List<GameItem>(); }
        }

        private static bool Is(GameItem g, string type)
        {
            try { return g.IsGameItemType(type); } catch { return false; }
        }

        private static GameItem FindItem(string type)
        {
            foreach (GameItem g in Carried()) if (Is(g, type)) return g;
            return null;
        }

        private static int AmmoCount()
        {
            int n = 0;
            foreach (GameItem g in Carried()) if (Is(g, "AMMUNITION")) n += Mathf.Max(1, g.unitCount);
            return n;
        }

        private static bool UseAmmo()
        {
            GameItem a = FindItem("AMMUNITION");
            if (a == null) return false;
            ConsumeOne(a);
            return true;
        }

        private static void ConsumeOne(GameItem g)
        {
            try
            {
                if (g.unitCount > 1) g.SetUnitCount(g.unitCount - 1);
                else { g.parentInventory?.Expel(g); g.Destroy(); }
            }
            catch (Exception ex) { Debug.LogWarning("[MapExtension] consume: " + ex.Message); }
        }

        private static string Name(GameItem g)
        {
            try { return g.GetDisplayName(); } catch { return g.identifier; }
        }

        // 가방에서 제일 센 무기. 수치는 게임에 남아 있는 옛 전투 기술(StandardCombatAbilityDirectory)을 참고한 대략값
        private static Weapon BestWeapon()
        {
            Weapon best = new Weapon { Name = "맨주먹", DmgMin = 3, DmgMax = 6, Acc = 85 };
            bool ammo = AmmoCount() > 0;
            foreach (GameItem g in Carried())
            {
                Weapon w = null;
                string id = (g.identifier ?? "").ToLowerInvariant();
                if (Is(g, "FIREARM") && ammo)
                {
                    if (id.Contains("shotgun")) w = new Weapon { DmgMin = 20, DmgMax = 30, Acc = 80 };
                    else if (id.Contains("mp9") || id.Contains("ump") || id.Contains("smg")) w = new Weapon { DmgMin = 9, DmgMax = 13, Acc = 75, Shots = 2 };
                    else if (id.Contains("rifle")) w = new Weapon { DmgMin = 22, DmgMax = 30, Acc = 80 };
                    else w = new Weapon { DmgMin = 14, DmgMax = 20, Acc = 75 };
                    w.Gun = true;
                }
                else if (Is(g, "MELEE_WEAPON") || (Is(g, "WEAPON") && !Is(g, "FIREARM")))
                {
                    if (id.Contains("machete") || id.Contains("cleaver")) w = new Weapon { DmgMin = 10, DmgMax = 15, Acc = 80, Bleed = true };
                    else if (id.Contains("knife")) w = new Weapon { DmgMin = 8, DmgMax = 12, Acc = 85, Bleed = true };
                    else if (id.Contains("bat") || id.Contains("pipe") || id.Contains("baton") || id.Contains("crowbar") || id.Contains("hammer"))
                        w = new Weapon { DmgMin = 9, DmgMax = 14, Acc = 80, Stun = true };
                    else w = new Weapon { DmgMin = 7, DmgMax = 12, Acc = 80 };
                }
                if (w == null) continue;
                w.Name = Name(g);
                if (Score(w) > Score(best)) best = w;
            }
            return best;
        }

        private static float Score(Weapon w) => (w.DmgMin + w.DmgMax) * 0.5f * w.Shots * w.Acc / 100f + (w.Bleed ? 3f : 0f) + (w.Stun ? 2f : 0f);

        // ---- 창 (자체 Canvas, 화면 맨 위) ----
        private static GameObject root;
        private static TextMeshProUGUI title, playerText, logText;
        private static readonly List<(GameObject go, Image bg, TextMeshProUGUI text)> foeButtons = new List<(GameObject, Image, TextMeshProUGUI)>();
        private static readonly List<Button> actionButtons = new List<Button>();
        private static GameObject closeBtn;

        private static readonly Color Panel = new Color32(0x1f, 0x1b, 0x2b, 0xf8);
        private static readonly Color Edge = new Color32(0xb4, 0x55, 0x3a, 0xff);
        private static readonly Color Btn = new Color32(0x2e, 0x29, 0x3e, 0xff);
        private static readonly Color BtnSel = new Color32(0x6a, 0x20, 0x22, 0xff);
        private static readonly Color Ink = new Color32(0xec, 0xe8, 0xf2, 0xff);

        internal static void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            foeButtons.Clear();
            actionButtons.Clear();
            closeBtn = null;
        }

        private static void Build()
        {
            root = new GameObject("VME_GridCombat", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<EscWatcher>();

            var dim = Rect(root.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // 뒤쪽 클릭 막기

            var panel = Rect(root.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-430f, -270f), new Vector2(430f, 270f));
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.color = Panel;
            var ol = panel.gameObject.AddComponent<Outline>();
            ol.effectColor = Edge;
            ol.effectDistance = new Vector2(4f, -4f);

            title = Text(panel, "Title", new Vector2(24f, -18f), new Vector2(812f, 36f), 26f, TextAlignmentOptions.Left);
            Set(title, "<color=#ff8a6b>전투</color>");

            // 적 (클릭 = 목표)
            for (int i = 0; i < foes.Count; i++)
            {
                int idx = i;
                var b = Rect(panel, "Foe" + i, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + i * 276f, -150f), new Vector2(24f + i * 276f + 260f, -66f));
                var img = b.gameObject.AddComponent<Image>();
                img.color = Btn;
                var btn = b.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => { if (!foes[idx].Dead) { target = idx; Refresh(); } });
                var t = Text(b, "Text", new Vector2(12f, -8f), new Vector2(236f, 70f), 20f, TextAlignmentOptions.TopLeft);
                foeButtons.Add((b.gameObject, img, t));
            }

            playerText = Text(panel, "Player", new Vector2(24f, -166f), new Vector2(812f, 60f), 20f, TextAlignmentOptions.TopLeft);
            logText = Text(panel, "Log", new Vector2(24f, -232f), new Vector2(812f, 220f), 18f, TextAlignmentOptions.TopLeft);
            logText.enableWordWrapping = true;

            string[] keys = { "attack", "defend", "heal", "flee" };
            string[] labels = { "공격", "방어", "치료", "도주" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                actionButtons.Add(MakeButton(panel, labels[i], new Vector2(24f + i * 206f, -470f), new Vector2(190f, 50f), () => Act(k)));
            }
            closeBtn = MakeButton(panel, "닫기", new Vector2(24f + 3 * 206f, -470f), new Vector2(190f, 50f), Finish).gameObject;
            closeBtn.SetActive(false);
        }

        private static void Refresh()
        {
            if (root == null) return;
            for (int i = 0; i < foeButtons.Count && i < foes.Count; i++)
            {
                Foe f = foes[i];
                var (go, bg, t) = foeButtons[i];
                bg.color = f.Dead ? new Color32(0x22, 0x20, 0x2a, 0xff) : i == target ? BtnSel : Btn;
                string state = f.Dead ? "<color=#80808a>쓰러짐</color>"
                    : "체력 " + f.Hp + "/" + f.MaxHp + (f.BleedTurns > 0 ? " <color=#ff5a5a>출혈</color>" : "") + (f.Stunned ? " <color=#ffcf6b>기절</color>" : "");
                Set(t, (i == target && !f.Dead ? "▶ " : "") + f.Name + "\n" + state + "\n<size=80%><color=#9a9aa8>공격 " + f.DmgMin + "~" + f.DmgMax + "</color></size>");
            }
            Weapon w = BestWeapon();
            int meds = 0;
            foreach (GameItem g in Carried()) if (Is(g, "MEDICAL")) meds += Mathf.Max(1, g.unitCount);
            string hpColor = hp > startHp * 0.5f ? "#4df0e0" : hp > startHp * 0.25f ? "#ffcf6b" : "#ff5a5a";
            Set(playerText, "나  체력 <color=" + hpColor + ">" + hp + "</color>/" + startHp
                            + "   무기 " + w.Name + " (" + w.DmgMin + "~" + w.DmgMax + (w.Shots > 1 ? " ×" + w.Shots : "") + ")"
                            + (w.Gun ? "   탄약 " + AmmoCount() : "")
                            + "\n<size=85%><color=#9a9aa8>방어구 " + (FindItem("ARMOR") != null ? "있음 (-30%)" : "없음") + "   의약품 " + meds + "</color></size>");
            Set(logText, string.Join("\n", log));
        }

        private static void SetButtons(bool on)
        {
            foreach (Button b in actionButtons) if (b != null) b.gameObject.SetActive(on);
        }

        // ---- UI 도우미 ----
        private static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        // pos = 부모 왼쪽 위 기준 (y 아래로 -)
        private static TextMeshProUGUI Text(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (FontLoader.Instance != null) t.font = FontLoader.Instance.notoSans_Normal ?? FontLoader.Instance.notoSans;
            t.fontSize = fontSize;
            t.color = Ink;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        private static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Action click)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = Btn;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = Edge;
            ol.effectDistance = new Vector2(2f, -2f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                try { EmporiumAudioPlayer.Instance.audioButton1.Play(); } catch { }
                try { click(); } catch (Exception ex) { Debug.LogWarning("[MapExtension] combat button: " + ex); }
            });
            var t = Text(rt, "Label", Vector2.zero, size, 22f, TextAlignmentOptions.Center);
            Set(t, label);
            return b;
        }

        private static void Set(TextMeshProUGUI t, string s)
        {
            if (t == null) return;
            if (string.IsNullOrEmpty(s)) { t.text = ""; return; }
            ScriptFontSwap.Apply(t, s);
        }
    }
}
