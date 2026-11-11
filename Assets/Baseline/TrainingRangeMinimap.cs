using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using TMPro;
using UnityEngine;

namespace MTPSKIT.UI.HUD
{
    public sealed class TrainingRangeMinimap : MonoBehaviour
    {
        bool facility;
        float Left => facility ? -14f : -13.62f;
        float Right => facility ? 22f : 13.61f;
        float Rear => facility ? -34f : -54.39f;
        float Front => facility ? 28f : 12.78f;
        const float Scale = 3.4f;
        float MapWidth => (Right - Left) * Scale;
        float MapHeight => (Front - Rear) * Scale;

        static readonly Color Floor = new Color(.15f, .21f, .26f);
        static readonly Color Guide = new Color(1f, .68f, .29f);
        static readonly Color Robot = new Color(1f, .35f, .3f);
        readonly List<RectTransform> robotDots = new List<RectTransform>(12);
        Transform layout;
        RectTransform map, playerDot;

        public void Initialize(Transform canvas, Transform rangeLayout, bool english)
        {
            layout = rangeLayout;
            facility = rangeLayout.name == "SciFi Training Facility";
            const float width = 248f;
            var panel = DrawBox(canvas, "Training minimap", 0, 0, width, 289, new Color(.04f, .06f, .085f, .93f));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-42f, -32f);
            FrontEndUI.Text(panel, english ? "TRAINING MAP" : "训练场小地图", 0, 4, width, 25, 18, TextAlignmentOptions.Center);
            map = DrawBox(panel, "Range outline", (width - MapWidth) / 2, 34, MapWidth, MapHeight,
                facility ? new Color(0f, 0f, 0f, 0f) : Floor);
            if (!facility)
            {
                DrawZone("Rear combat area", Left, Rear, Right - Left, -9.4f - Rear, new Color(.29f,.23f,.24f));
                DrawZone("Left through corridor", Left, -9.4f, -4.15f - Left, 16.52f, new Color(.20f,.31f,.39f));
                DrawZone("Right front room", -4.15f, -9.4f, 17.75f, 16.37f, new Color(.25f,.29f,.33f));
                DrawZone("Left baffle west jamb", -10.73f, 7.04f, 2.33f, .2f, Color.gray);
                DrawZone("Left baffle east jamb", -6.2f, 7.04f, 2.03f, .2f, Color.gray);
                DrawZone("Front window wall west jamb", -4.15f, 6.9f, 1.05f, .2f, Color.gray);
                DrawZone("Front window wall", -1.6f, 6.9f, 15.2f, .2f, Color.gray);
                DrawZone("GM console platform", 4.9f, -10.8f, 4.8f, 2.8f, Guide);
                DrawZone("Room corridor partition south", -4.26f, -9.4f, .22f, 4.85f, Color.gray);
                DrawZone("Room corridor partition north", -4.26f, -2.25f, .22f, 9.22f, Color.gray);
            }
            else
            {
                DrawZone(english ? "Combat" : "战斗区", -12, -34, 34, 34, new Color(.36f, .24f, .24f), true);
                DrawZone(english ? "Prep" : "整备", -13.5f, 4.5f, 13.5f, 9, new Color(.23f, .36f, .39f), true);
                DrawZone(english ? "Weapons" : "武器室", 0, 4.5f, 9, 9, new Color(.26f, .33f, .43f), true);
                DrawZone(english ? "Living" : "生活区", -5, 13.5f, 14.15f, 13.83f, new Color(.35f, .33f, .27f), true);
                DrawZone("Entry passage", 0, 0, 9, 4.5f, new Color(.25f,.27f,.3f));
                var station = layout.Find("整备室/Supply and GM station");
                if (station) { var p = layout.InverseTransformPoint(station.position); DrawZone("Supply table", p.x - .6f, p.z - .4f, 1.2f, .8f, Guide); }
            }

            var legend = FrontEndUI.Text(panel, english ? "Blue: You   Red: Bots" : "蓝：自己　红：机器人",
                0, 269, width, 18, 14, TextAlignmentOptions.Center);
            legend.color = new Color(.78f, .86f, .92f);
            playerDot = Marker("Player", 8, new Color(.38f, .85f, 1f));
            DrawBox(playerDot, "Heading", 3, -5, 2, 6, Color.white);
        }

        void DrawZone(string name, float x, float z, float width, float depth, Color tint, bool label = false)
        {
            var zone = DrawBox(map, name, (x - Left) * Scale, (Front - z - depth) * Scale,
                width * Scale, depth * Scale, tint);
            if (label)
                FrontEndUI.Text(zone, name, 0, depth * Scale / 2 - 8, width * Scale, 16, 11, TextAlignmentOptions.Center);
        }

        void LateUpdate()
        {
            if (!layout || !map) return;
            var player = GameManager.myPlayerInstance;
            var character = player ? player.MyCharacter : null;
            bool visible = character && character.CurrentHealth > 0 && SetPosition(playerDot, character.transform.position);
            playerDot.gameObject.SetActive(visible);
            if (visible)
            {
                var direction = layout.InverseTransformDirection(character.transform.forward);
                playerDot.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            }

            int count = 0;
            foreach (var identity in NetworkClient.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent<PlayerInstance>(out var bot) || !bot.BOT ||
                    !bot.MyCharacter || bot.MyCharacter.CurrentHealth <= 0) continue;
                if (count == robotDots.Count) robotDots.Add(Marker("Robot", 6, Robot));
                var dot = robotDots[count++];
                dot.gameObject.SetActive(SetPosition(dot, bot.MyCharacter.transform.position));
            }
            for (int i = count; i < robotDots.Count; i++) robotDots[i].gameObject.SetActive(false);
        }

        Vector2 Project(Vector3 world)
        {
            var local = layout.InverseTransformPoint(world);
            return new Vector2((local.x - Left) * Scale, (Front - local.z) * Scale);
        }

        bool SetPosition(RectTransform marker, Vector3 world)
        {
            var point = Project(world);
            if (point.x < 0 || point.x > MapWidth || point.y < 0 || point.y > MapHeight) return false;
            marker.anchoredPosition = new Vector2(point.x, -point.y);
            return true;
        }

        RectTransform Marker(string name, float size, Color color)
        {
            var dot = DrawBox(map, name, 0, 0, size, size, color);
            dot.pivot = new Vector2(.5f, .5f);
            return dot;
        }

        static RectTransform DrawBox(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = FrontEndUI.Box(parent, name, x, y, w, h, color);
            image.raycastTarget = false;
            return image.rectTransform;
        }
    }
}

