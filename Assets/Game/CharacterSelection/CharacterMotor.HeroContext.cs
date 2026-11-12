using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        PlayerInstance heroPlayer;
        Gun profileWeapon;
        bool profileResolved;
        WendyRifleProfile weaponProfile;
        readonly WendyRifleProfile[] weaponProfiles = new WendyRifleProfile[4];
        bool profilesLoaded;
        internal int WeaponProfileResolutions { get; private set; }
        internal void BindHeroPlayer(PlayerInstance player) => heroPlayer = player;
        internal int CurrentHeroSelection => heroPlayer && heroPlayer.MyCharacter == _charInstance ? heroPlayer.HeroSelection : -1;
        internal bool UsesFourWayRoll => _charInstance && !_charInstance.BOT &&
            GameManager.Gamemode is Gamemodes.RangeGamemode && heroPlayer && !heroPlayer.BOT &&
            heroPlayer.MyCharacter == _charInstance && heroPlayer.HasHeroLoadout;

        // Gameplay resolves the current equipment, never the display graph. Null matches are cached too.
        internal WendyRifleProfile SprintProfile
        {
            get
            {
                var item = _charInstance && _charInstance.CharacterItemManager
                    ? _charInstance.CharacterItemManager.CurrentlyUsedItem as Gun : null;
                if (!item)
                {
                    if (!profileResolved || !ReferenceEquals(profileWeapon, null)) WeaponProfileResolutions++;
                    profileResolved = true; profileWeapon = null; weaponProfile = null;
                    return null;
                }
                if (profileResolved && ReferenceEquals(profileWeapon, item)) return weaponProfile;
                profileResolved = true; profileWeapon = item; weaponProfile = null;
                WeaponProfileResolutions++;
                if (!item || !item.AaaBinding) return null;
                if (!profilesLoaded)
                {
                    weaponProfiles[0] = Resources.Load<WendyRifleProfile>("DemonHunter3C/WendyRifle");
                    weaponProfiles[1] = Resources.Load<WendyRifleProfile>("DemonHunter3C/WendyPistol");
                    weaponProfiles[2] = Resources.Load<WendyRifleProfile>("DemonHunter3C/WendyShotgun");
                    weaponProfiles[3] = Resources.Load<WendyRifleProfile>("DemonHunter3C/WendySniper");
                    profilesLoaded = true;
                }
                string itemName = item.name;
                for (int i = 0; i < weaponProfiles.Length; i++)
                {
                    var candidate = weaponProfiles[i];
                    if (!candidate) continue;
                    string key = candidate.weaponResource;
                    if (itemName == key || itemName.Length == key.Length + 7 &&
                        itemName.StartsWith(key, System.StringComparison.Ordinal) &&
                        itemName.EndsWith("(Clone)", System.StringComparison.Ordinal))
                        return weaponProfile = candidate;
                }
                return null;
            }
        }
    }

    // Snapshot correction is consumed gradually, never by jumping the animation/physics clock.
    internal struct HeroActionClock
    {
        internal float Time { get; private set; }
        float lastSnapshot, correction;
        internal void Reset(float time = 0f) { Time = lastSnapshot = time; correction = 0f; }
        internal void Observe(float snapshot)
        {
            if (snapshot == lastSnapshot) return;
            lastSnapshot = snapshot;
            correction = snapshot - Time;
        }
        internal void Advance(float delta)
        {
            if (delta <= 0f) return;
            float adjustment = Mathf.Clamp(correction, -delta * .25f, delta * .25f);
            correction -= adjustment;
            Time += delta + adjustment;
        }
    }
}
