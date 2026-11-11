namespace AvatarPortability.Expressions
{
    /// <summary>Display labels only. Author names, animation assets and GUIDs remain unchanged.</summary>
    public static class WendyExpressionChinese
    {
        public static string Name(string originalName)
        {
            switch (originalName)
            {
                case "Blink_Enable": return "自动眨眼";
                case "FaceEmote_AFK end": return "结束离开";
                case "FaceEmote_AFK start": return "进入离开";
                case "FaceEmote_AFK": return "离开状态";
                case "FaceEmote_Default": return "默认表情";
                case "FaceEmote_Override_NadeNade": return "被摸头";
                case "あは": return "开怀笑";
                case "ぱぁうるうる": return "感动含泪";
                case "ふーん": return "不以为意";
                case "むー！": return "鼓嘴不满";
                case "キス顔": return "亲吻";
                case "ヤンデレハート": return "病娇爱心";
                case "洗脳ハート": return "洗脑爱心";
                case "しゅん": return "垂头丧气";
                case "すねる": return "委屈闹别扭";
                case "涙": return "流泪";
                case "耐える": return "强忍泪水";
                case "はん！": return "轻哼得意";
                case "キラキラ": return "闪亮双眼";
                case "ニヤリ顔影": return "阴影坏笑";
                case "照れ": return "害羞";
                case "笑顔": return "微笑";
                case "おこ": return "生气";
                case "ぐう…！": return "咬牙忍耐";
                case "はぁ？": return "不满质问";
                case "ぷんぷん！": return "气鼓鼓";
                case "めっ！": return "严肃责备";
                case "引き顔": return "嫌弃退缩";
                case "本当に嫌": return "十分厌恶";
                case "えへへ～": return "甜甜傻笑";
                case "てへぺろ": return "吐舌卖萌";
                case "どや！": return "得意扬扬";
                case "にしし": return "俏皮偷笑";
                case "ほのぼの": return "温柔安心";
                case "ウィンク": return "眨眼";
                case "あ…あ…": return "紧张结巴";
                case "えっとぉ～汗": return "尴尬冒汗";
                case "ぐるぐる": return "头晕目眩";
                case "ふぇっ！？": return "吃惊";
                case "MouthMorphCanceller_ENABLE": return "口型抑制";
                default: return "未命名表情";
            }
        }

        public static string Category(string originalCategory)
        {
            switch (originalCategory)
            {
                case "etc": return "其他表情";
                case "喜": return "喜悦";
                case "怒": return "生气";
                case "哀": return "悲伤";
                case "楽": return "开心";
                case "焦": return "紧张";
                case "驚": return "惊讶";
                case "辅助/Blink": return "辅助·眨眼";
                case "辅助/AFK": return "辅助·离开状态";
                case "辅助/FaceEmote": return "辅助·默认与互动";
                case "辅助/MouthMorphCanceller": return "辅助·口型抑制";
                default: return "其他表情";
            }
        }
    }
}

